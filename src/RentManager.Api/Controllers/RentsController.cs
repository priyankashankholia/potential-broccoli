using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
using RentManager.Api.Data;
using RentManager.Api.Services;

namespace RentManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/rents")]
public class RentsController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly RentGenerationService _rentGenerator;

    public RentsController(
        RentManagerDbContext db,
        RentGenerationService rentGenerator)
    {
        _db = db;
        _rentGenerator = rentGenerator;
    }

    // There is no POST /api/rents/generate any more. Rent creation is
    // handled by RentGenerationBackgroundService.
    [HttpGet]
    public async Task<IActionResult> GetRents()
    {
        // Safety net in case the process has been running since before
        // midnight on the 1st, so we do not wait for the hourly timer.
        await _rentGenerator.EnsureRentsUpToCurrentMonthAsync();

        var today = IndiaClock.Today();

        var rents = await _db.Rents
            .Include(r => r.Tenant)
            .Where(r => r.Tenant.IsActive)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .ToListAsync();

        var result = rents.Select(r =>
        {
            var status = RentStatusCalculator.For(r, today);

            return new
            {
                r.Id,
                r.TenantId,
                TenantName = r.Tenant.Name,
                r.Year,
                r.Month,
                MonthLabel = IndiaClock.MonthLabel(r.Year, r.Month),
                r.AmountDue,
                r.AmountPaid,
                Remaining = status.Remaining,
                r.DueDate,
                r.IsSettled,
                status.Status,
                status.Timing,
                status.DaysUntilDue,
                status.IsDueSoon,
                status.IsPayable
            };
        });

        return Ok(result);
    }

    // Everything the Manage Rent screen needs in one request: current
    // month, cumulative payable, and the full history with each month's
    // payments attached. The old screen fired one request per month.
    [HttpGet("tenant/{tenantId:int}/ledger")]
    public async Task<IActionResult> GetTenantLedger(int tenantId)
    {
        await _rentGenerator.EnsureRentsUpToCurrentMonthAsync(tenantId);

        var tenant = await _db.Tenants
            .Include(t => t.Shop)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant is null)
        {
            return ApiResults.Missing("Tenant not found.");
        }

        var today = IndiaClock.Today();
        var currentKey = IndiaClock.MonthKey(today.Year, today.Month);

        var rents = await _db.Rents
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Payments)
            .ToListAsync();

        var ordered = rents
            .OrderByDescending(r => IndiaClock.MonthKey(r.Year, r.Month))
            .ToList();

        var currentRent = ordered.FirstOrDefault(r =>
            r.Year == today.Year && r.Month == today.Month);

        // Only collectable months count towards the total.
        var payable = ordered
            .Where(r => RentStatusCalculator.IsPayable(r, today))
            .ToList();

        var totalPayable = payable
            .Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));

        var previousOutstanding = payable
            .Where(r => IndiaClock.MonthKey(r.Year, r.Month) < currentKey)
            .Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));

        var currentStatus = currentRent is null
            ? null
            : RentStatusCalculator.For(currentRent, today);

        var history = ordered.Select(r =>
        {
            var status = RentStatusCalculator.For(r, today);

            return new
            {
                r.Id,
                r.Year,
                r.Month,
                MonthLabel = IndiaClock.MonthLabel(r.Year, r.Month),
                r.AmountDue,
                r.AmountPaid,
                Remaining = status.Remaining,
                r.DueDate,
                r.IsSettled,
                status.Status,
                status.Timing,
                status.DaysUntilDue,
                status.IsDueSoon,
                status.IsPayable,
                Payments = r.Payments
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
            };
        });

        var nextMonth = IndiaClock.AddMonths(today.Year, today.Month, 1);

        // The next month that exists but is not collectable yet, so the
        // screen can say when the next payment is expected.
        var nextUpcoming = ordered
            .Where(r =>
                !r.IsSettled &&
                !RentStatusCalculator.IsPayable(r, today))
            .OrderBy(r => IndiaClock.MonthKey(r.Year, r.Month))
            .FirstOrDefault();

        return Ok(new
        {
            Tenant = new
            {
                tenant.Id,
                tenant.Name,
                tenant.MobileNumber,
                tenant.MonthlyRent,
                tenant.RentDueDay,
                ShopName = tenant.Shop?.Name
            },

            Today = today,

            CurrentMonth = currentRent is null
                ? null
                : new
                {
                    RentId = currentRent.Id,
                    currentRent.Year,
                    currentRent.Month,
                    MonthLabel = IndiaClock.MonthLabel(currentRent.Year, currentRent.Month),
                    currentRent.AmountDue,
                    currentRent.AmountPaid,
                    Remaining = currentStatus!.Remaining,
                    currentRent.DueDate,
                    currentStatus.Status,
                    currentStatus.Timing,
                    currentStatus.DaysUntilDue,
                    currentStatus.IsDueSoon,
                    currentStatus.IsPayable
                },

            PreviousOutstanding = previousOutstanding,

            // The single "Total to be Paid" figure.
            TotalPayable = totalPayable,

            NextExpected = nextUpcoming is null
                ? null
                : new
                {
                    MonthLabel = IndiaClock.MonthLabel(nextUpcoming.Year, nextUpcoming.Month),
                    nextUpcoming.AmountDue,
                    nextUpcoming.DueDate,
                    Timing = RentStatusCalculator.For(nextUpcoming, today).Timing
                },

            UpcomingRentAmount = currentRent is not null &&
                                 currentRent.AmountDue != tenant.MonthlyRent
                ? new
                {
                    Amount = tenant.MonthlyRent,
                    EffectiveFrom = IndiaClock.MonthLabel(nextMonth.Year, nextMonth.Month)
                }
                : null,

            History = history
        });
    }
}
