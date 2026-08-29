using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
using RentManager.Api.Data;
using RentManager.Api.Models;
using RentManager.Api.Services;

namespace RentManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly RentLedgerService _ledger;

    public PaymentsController(RentManagerDbContext db, RentLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    [HttpGet("rent/{rentId:int}")]
    public async Task<IActionResult> GetPayments(int rentId)
    {
        var payments = await _db.Payments
            .Where(p => p.RentId == rentId)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.RentId,
                p.Amount,
                p.PaymentDate,
                p.PaymentMode,
                p.Note
            })
            .ToListAsync();

        return Ok(payments);
    }

    // One payment can settle several months. It is applied to the oldest
    // collectable month first, which is what clears carried-forward
    // balances in the right order.
    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return ApiResults.Invalid("Payment amount must be greater than zero.");
        }

        var tenantId = request.TenantId;

        if (tenantId <= 0 && request.RentId is > 0)
        {
            var selected = await _db.Rents
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.RentId!.Value);

            if (selected is null)
            {
                return ApiResults.Invalid("Rent record not found.");
            }

            tenantId = selected.TenantId;
        }

        if (tenantId <= 0)
        {
            return ApiResults.Invalid("Tenant is required.");
        }

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);

        if (tenant is null)
        {
            return ApiResults.Invalid("Tenant not found.");
        }

        // Same eligibility rule the ledger screen uses, so what is shown
        // as payable is exactly what can be paid.
        var payableRents = await _ledger.GetPayableRentsAsync(tenantId);

        var totalPayable = payableRents
            .Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));

        if (totalPayable <= 0)
        {
            return ApiResults.Invalid(
                $"{tenant.Name} has no pending amount right now.");
        }

        if (request.Amount > totalPayable)
        {
            return ApiResults.Invalid(
                $"Amount cannot be more than the total payable of Rs {totalPayable:N0}.");
        }

        var paymentDate = request.PaymentDate ?? IndiaClock.Today();

        var remainingToAllocate = request.Amount;
        var created = new List<Payment>();

        foreach (var rent in payableRents)
        {
            if (remainingToAllocate <= 0)
            {
                break;
            }

            var rentOutstanding = Math.Max(0m, rent.AmountDue - rent.AmountPaid);
            var allocation = Math.Min(remainingToAllocate, rentOutstanding);

            if (allocation <= 0)
            {
                continue;
            }

            var payment = new Payment
            {
                RentId = rent.Id,
                Amount = allocation,
                PaymentDate = paymentDate,
                PaymentMode = string.IsNullOrWhiteSpace(request.PaymentMode)
                    ? "Cash"
                    : request.PaymentMode.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note)
                    ? null
                    : request.Note.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            created.Add(payment);

            remainingToAllocate -= allocation;
        }

        await _db.SaveChangesAsync();

        await _ledger.RecalculateAsync(
            created.Select(p => p.RentId).Distinct().ToList());

        var remainingOutstanding = totalPayable - request.Amount;
        var fullyPaid = remainingOutstanding <= 0;

        _db.Notifications.Add(new Notification
        {
            TenantId = tenantId,
            RentId = created.FirstOrDefault()?.RentId,
            Type = "PaymentReceived",
            Channel = "WhatsApp",
            Message = fullyPaid
                ? $"Payment of Rs {request.Amount:N0} received. All dues are now cleared. Thank you."
                : $"Payment of Rs {request.Amount:N0} received. Remaining balance: Rs {remainingOutstanding:N0}.",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            TotalAmount = request.Amount,
            RemainingOutstanding = remainingOutstanding,
            FullyPaid = fullyPaid,
            Allocations = created.Select(p => new
            {
                p.Id,
                p.RentId,
                p.Amount,
                p.PaymentDate,
                p.PaymentMode
            })
        });
    }

    // Correcting a payment re-derives that month's paid amount from its
    // payment rows, so the month balance and the cumulative balance both
    // land on the right number.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePayment(
        int id,
        [FromBody] UpdatePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return ApiResults.Invalid("Payment amount must be greater than zero.");
        }

        var payment = await _db.Payments
            .Include(p => p.Rent)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
        {
            return ApiResults.Missing("Payment not found.");
        }

        var rent = payment.Rent;

        // What the month's balance would be with this payment removed.
        var otherPayments = await _db.Payments
            .Where(p => p.RentId == rent.Id && p.Id != id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var maxAllowed = Math.Max(0m, rent.AmountDue - otherPayments);

        if (request.Amount > maxAllowed)
        {
            return ApiResults.Invalid(
                $"Amount cannot be more than Rs {maxAllowed:N0} for this month.");
        }

        payment.Amount = request.Amount;

        if (request.PaymentDate.HasValue)
        {
            payment.PaymentDate = request.PaymentDate.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentMode))
        {
            payment.PaymentMode = request.PaymentMode.Trim();
        }

        payment.Note = string.IsNullOrWhiteSpace(request.Note)
            ? null
            : request.Note.Trim();

        await _db.SaveChangesAsync();

        await _ledger.RecalculateAsync(new[] { rent.Id });

        await _db.SaveChangesAsync();

        var tenantOutstanding = await _ledger.GetTotalPayableAsync(rent.TenantId);

        return Ok(new
        {
            payment.Id,
            payment.RentId,
            payment.Amount,
            payment.PaymentDate,
            payment.PaymentMode,
            payment.Note,
            Rent = new
            {
                rent.Id,
                rent.AmountDue,
                rent.AmountPaid,
                Remaining = Math.Max(0m, rent.AmountDue - rent.AmountPaid),
                rent.IsSettled
            },
            TotalPayable = tenantOutstanding
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Rent)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
        {
            return ApiResults.Missing("Payment not found.");
        }

        var rentId = payment.RentId;
        var tenantId = payment.Rent.TenantId;

        _db.Payments.Remove(payment);

        await _db.SaveChangesAsync();

        await _ledger.RecalculateAsync(new[] { rentId });

        await _db.SaveChangesAsync();

        var rent = await _db.Rents.FirstAsync(r => r.Id == rentId);

        var tenantOutstanding = await _ledger.GetTotalPayableAsync(tenantId);

        return Ok(new
        {
            Message = "Payment deleted and balance recalculated.",
            Rent = new
            {
                rent.Id,
                rent.AmountDue,
                rent.AmountPaid,
                Remaining = Math.Max(0m, rent.AmountDue - rent.AmountPaid),
                rent.IsSettled
            },
            TotalPayable = tenantOutstanding
        });
    }
}

public class CreatePaymentRequest
{
    public int TenantId { get; set; }

    public int? RentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly? PaymentDate { get; set; }

    public string? PaymentMode { get; set; }

    public string? Note { get; set; }
}

public class UpdatePaymentRequest
{
    public decimal Amount { get; set; }

    public DateOnly? PaymentDate { get; set; }

    public string? PaymentMode { get; set; }

    public string? Note { get; set; }
}
