using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public PaymentsController(RentManagerDbContext db)
    {
        _db = db;
    }

    [HttpGet("rent/{rentId}")]
    public async Task<IActionResult> GetPayments(int rentId)
    {
        var payments = await _db.Payments
            .Where(p => p.RentId == rentId)
            .OrderByDescending(p => p.PaymentDate)
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

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request)
    {
        var rent = await _db.Rents
            .FirstOrDefaultAsync(r => r.Id == request.RentId);

        if (rent is null)
        {
            return BadRequest("Rent not found.");
        }

        if (rent.IsSettled)
        {
            return BadRequest("This rent is already fully paid.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Payment amount must be greater than zero.");
        }

        var remaining = rent.AmountDue - rent.AmountPaid;

        if (request.Amount > remaining)
        {
            return BadRequest(
                $"Payment cannot exceed the remaining balance of {remaining}.");
        }

        var payment = new Payment
        {
            RentId = rent.Id,
            Amount = request.Amount,
            PaymentDate = DateTime.UtcNow,
            PaymentMode = request.PaymentMode ?? "Cash",
            Note = request.Note
        };

        rent.AmountPaid += request.Amount;
        rent.IsSettled = rent.AmountPaid >= rent.AmountDue;

        _db.Payments.Add(payment);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            payment.Id,
            payment.RentId,
            payment.Amount,
            payment.PaymentDate,
            payment.PaymentMode,
            rent.AmountDue,
            rent.AmountPaid,
            Remaining = rent.AmountDue - rent.AmountPaid,
            rent.IsSettled
        });
    }
}

public class CreatePaymentRequest
{
    public int RentId { get; set; }

    public decimal Amount { get; set; }

    public string? PaymentMode { get; set; }

    public string? Note { get; set; }
}