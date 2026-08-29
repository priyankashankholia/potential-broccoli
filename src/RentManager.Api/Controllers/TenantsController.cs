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
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly RentGenerationService _rentGenerator;
    private readonly RentLedgerService _ledger;

    public TenantsController(
        RentManagerDbContext db,
        RentGenerationService rentGenerator,
        RentLedgerService ledger)
    {
        _db = db;
        _rentGenerator = rentGenerator;
        _ledger = ledger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _db.Tenants
            .Include(t => t.Shop)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.MobileNumber,
                t.PanCard,
                t.MonthlyRent,
                t.RentDueDay,
                t.SecurityDeposit,
                t.RentStartYear,
                t.RentStartMonth,
                t.IsActive,
                Shop = t.Shop == null ? null : new { t.Shop.Id, t.Shop.Name }
            })
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTenant(int id)
    {
        var tenant = await _db.Tenants
            .Include(t => t.Shop)
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.MobileNumber,
                t.PanCard,
                t.MonthlyRent,
                t.RentDueDay,
                t.SecurityDeposit,
                t.RentStartYear,
                t.RentStartMonth,
                t.IsActive,
                Shop = t.Shop == null ? null : new { t.Shop.Id, t.Shop.Name }
            })
            .FirstOrDefaultAsync();

        if (tenant is null)
        {
            return ApiResults.Missing("Tenant not found.");
        }

        return Ok(tenant);
    }

    // Feeds the two radio buttons on the Add Tenant form.
    //
    // The first option is the first month whose due date has not already
    // passed, not simply the current calendar month. On 27 August with a
    // due day of 1 that is September, because 1 August is behind us.
    [HttpGet("first-rent-options")]
    public IActionResult GetFirstRentOptions([FromQuery] int rentDueDay = 1)
    {
        if (rentDueDay < 1 || rentDueDay > 31)
        {
            return ApiResults.Invalid("Rent due day must be between 1 and 31.");
        }

        var today = IndiaClock.Today();

        var next = IndiaClock.AddMonths(today.Year, today.Month, 1);

        var normalDue = IndiaClock.DueDateFor(today.Year, today.Month, rentDueDay);
        var backdated = normalDue < today;

        return Ok(new
        {
            Current = new
            {
                Year = today.Year,
                Month = today.Month,
                Label = IndiaClock.MonthLabel(today.Year, today.Month),
                DueDate = backdated ? today : normalDue,
                IsBackdated = backdated
            },
            Next = Describe(next.Year, next.Month, rentDueDay)
        });

        static object Describe(int year, int month, int dueDay)
        {
            return new
            {
                Year = year,
                Month = month,
                Label = IndiaClock.MonthLabel(year, month),
                DueDate = IndiaClock.DueDateFor(year, month, dueDay)
            };
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var validation = Validate(
            request.Name,
            request.MobileNumber,
            request.MonthlyRent,
            request.RentDueDay);

        if (validation is not null)
        {
            return validation;
        }

        var shop = await _db.Shops
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.IsActive);

        if (shop is null)
        {
            return ApiResults.Invalid("Shop not found.");
        }

        if (shop.Tenant is { IsActive: true })
        {
            return ApiResults.Blocked(
                $"{shop.Name} already has {shop.Tenant.Name} assigned.");
        }

        var today = IndiaClock.Today();

        // "Current" now means the actual current calendar month, even when
        // its due day has gone by. That month's due date becomes today, so
        // the landlord can collect it straight away rather than seeing it
        // appear already overdue.
        (int Year, int Month) start;
        DateOnly? firstDue = null;

        if (string.Equals(request.FirstRentMonth, "Next", StringComparison.OrdinalIgnoreCase))
        {
            start = IndiaClock.AddMonths(today.Year, today.Month, 1);
        }
        else
        {
            start = (today.Year, today.Month);

            var normalDue = IndiaClock.DueDateFor(
                today.Year,
                today.Month,
                request.RentDueDay);

            if (normalDue < today)
            {
                firstDue = today;
            }
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            MobileNumber = request.MobileNumber.Trim(),
            PanCard = string.IsNullOrWhiteSpace(request.PanCard)
                ? null
                : request.PanCard.Trim(),
            MonthlyRent = request.MonthlyRent,
            RentDueDay = request.RentDueDay,
            SecurityDeposit = request.SecurityDeposit,
            RentStartYear = start.Year,
            RentStartMonth = start.Month,
            FirstDueDate = firstDue,
            ShopId = request.ShopId,
            IsActive = true
        };

        _db.Tenants.Add(tenant);

        await _db.SaveChangesAsync();

        // Only creates something if the start month is the current month
        // or earlier. A future start month is picked up automatically when
        // that month arrives.
        await _rentGenerator.EnsureRentsUpToCurrentMonthAsync(tenant.Id);

        return Created($"/api/tenants/{tenant.Id}", new
        {
            tenant.Id,
            tenant.Name,
            tenant.MobileNumber,
            tenant.MonthlyRent,
            tenant.RentDueDay,
            tenant.ShopId,
            FirstRentMonth = IndiaClock.MonthLabel(start.Year, start.Month),
            FirstDueDate = firstDue
                ?? IndiaClock.DueDateFor(start.Year, start.Month, tenant.RentDueDay)
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTenant(
        int id,
        [FromBody] UpdateTenantRequest request)
    {
        var validation = Validate(
            request.Name,
            request.MobileNumber,
            request.MonthlyRent,
            request.RentDueDay);

        if (validation is not null)
        {
            return validation;
        }

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        if (tenant is null)
        {
            return ApiResults.Missing("Tenant not found.");
        }

        if (tenant.ShopId != request.ShopId)
        {
            var shop = await _db.Shops
                .Include(s => s.Tenant)
                .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.IsActive);

            if (shop is null)
            {
                return ApiResults.Invalid("Shop not found.");
            }

            if (shop.Tenant is { IsActive: true } other && other.Id != tenant.Id)
            {
                return ApiResults.Blocked(
                    $"{shop.Name} already has {other.Name} assigned.");
            }

            tenant.ShopId = request.ShopId;
        }

        var today = IndiaClock.Today();

        var currentRent = await _db.Rents
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenant.Id &&
                r.Year == today.Year &&
                r.Month == today.Month);

        var dueDayChanged = tenant.RentDueDay != request.RentDueDay;

        // Current month fully paid: the new day starts next month.
        // Current month unpaid or partly paid: it moves right away.
        var dueDayAppliedNow =
            dueDayChanged && currentRent is not null && !currentRent.IsSettled;

        if (dueDayAppliedNow)
        {
            currentRent!.DueDate = IndiaClock.DueDateFor(
                currentRent.Year,
                currentRent.Month,
                request.RentDueDay);
        }

        // The new amount is stored on the tenant and picked up by the next
        // generated month. Existing months keep what they were generated
        // with, so a rent change never rewrites an in-flight month.
        var rentChanged = tenant.MonthlyRent != request.MonthlyRent;

        tenant.Name = request.Name.Trim();
        tenant.MobileNumber = request.MobileNumber.Trim();
        tenant.PanCard = string.IsNullOrWhiteSpace(request.PanCard)
            ? null
            : request.PanCard.Trim();
        tenant.MonthlyRent = request.MonthlyRent;
        tenant.RentDueDay = request.RentDueDay;
        tenant.SecurityDeposit = request.SecurityDeposit;

        await _db.SaveChangesAsync();

        var nextMonth = IndiaClock.AddMonths(today.Year, today.Month, 1);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.MobileNumber,
            tenant.PanCard,
            tenant.MonthlyRent,
            tenant.RentDueDay,
            tenant.SecurityDeposit,
            tenant.ShopId,
            tenant.IsActive,
            RentChangeEffectiveFrom = rentChanged
                ? IndiaClock.MonthLabel(nextMonth.Year, nextMonth.Month)
                : null,
            DueDayAppliedToCurrentMonth = dueDayAppliedNow
        });
    }

    // Deactivated, never deleted, so rent and payment history survives.
    // Blocked while any collectable rent still has a balance.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTenant(int id)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null)
        {
            return ApiResults.Missing("Tenant not found.");
        }

        if (!tenant.IsActive)
        {
            return ApiResults.Invalid("This tenant has already been removed.");
        }

        var outstanding = await _ledger.GetTotalPayableAsync(id);

        if (outstanding > 0)
        {
            return ApiResults.Blocked(
                $"{tenant.Name} cannot be removed yet. Unpaid dues of " +
                $"Rs {outstanding:N0} are still pending. Please clear the dues " +
                "first, all rent history will be preserved.");
        }

        tenant.IsActive = false;
        tenant.ShopId = null;

        // Records when they left, which is what stops rent generating for
        // months after the shop was handed back.
        tenant.LeaseEndDate = IndiaClock.Today().ToDateTime(TimeOnly.MinValue);

        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static IActionResult? Validate(
        string name,
        string mobileNumber,
        decimal monthlyRent,
        int rentDueDay)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiResults.Invalid("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            return ApiResults.Invalid("Mobile number is required.");
        }

        if (monthlyRent <= 0)
        {
            return ApiResults.Invalid("Monthly rent must be greater than zero.");
        }

        if (rentDueDay < 1 || rentDueDay > 31)
        {
            return ApiResults.Invalid("Rent due day must be between 1 and 31.");
        }

        return null;
    }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? PanCard { get; set; }

    public decimal MonthlyRent { get; set; }

    public int RentDueDay { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public int ShopId { get; set; }

    // "Current" or "Next", relative to the first applicable month.
    public string FirstRentMonth { get; set; } = "Current";
}

public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? PanCard { get; set; }

    public decimal MonthlyRent { get; set; }

    public int RentDueDay { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public int ShopId { get; set; }
}
