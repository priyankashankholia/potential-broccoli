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
[Route("api/shops")]
public class ShopsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public ShopsController(RentManagerDbContext db)
    {
        _db = db;
    }

    // Active shops plus everything the dashboard card needs, including the
    // tenant's rent status and cumulative payable.
    [HttpGet]
    public async Task<IActionResult> GetShops()
    {
        var today = IndiaClock.Today();
        var currentKey = IndiaClock.MonthKey(today.Year, today.Month);

        var shops = await _db.Shops
            .Where(s => s.IsActive)
            .Include(s => s.Tenant)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var tenantIds = shops
            .Where(s => s.Tenant != null && s.Tenant.IsActive)
            .Select(s => s.Tenant!.Id)
            .ToList();

        var rents = await _db.Rents
            .Where(r => tenantIds.Contains(r.TenantId))
            .ToListAsync();

        var result = shops.Select(shop =>
        {
            var tenant = shop.Tenant is { IsActive: true } ? shop.Tenant : null;

            if (tenant is null)
            {
                return new
                {
                    shop.Id,
                    shop.Name,
                    IsOccupied = false,
                    Tenant = (object?)null,
                    Rent = (object?)null
                };
            }

            var tenantRents = rents.Where(r => r.TenantId == tenant.Id).ToList();

            var currentRent = tenantRents.FirstOrDefault(r =>
                r.Year == today.Year && r.Month == today.Month);

            // Only months that are actually collectable count towards the
            // total, so a rent 20 days away is not shown as owed.
            var totalPayable = tenantRents
                .Where(r => RentStatusCalculator.IsPayable(r, today))
                .Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));

            var previousOutstanding = tenantRents
                .Where(r =>
                    IndiaClock.MonthKey(r.Year, r.Month) < currentKey &&
                    RentStatusCalculator.IsPayable(r, today))
                .Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));

            var status = currentRent is null
                ? null
                : RentStatusCalculator.For(currentRent, today);

            // If this month has no rent yet, show the next generated month
            // so the card is not blank.
            var upcomingRent = currentRent is null
                ? tenantRents
                    .Where(r => IndiaClock.MonthKey(r.Year, r.Month) > currentKey)
                    .OrderBy(r => IndiaClock.MonthKey(r.Year, r.Month))
                    .FirstOrDefault()
                : null;

            var displayRent = currentRent ?? upcomingRent;

            var displayStatus = displayRent is null
                ? null
                : RentStatusCalculator.For(displayRent, today);

            return new
            {
                shop.Id,
                shop.Name,
                IsOccupied = true,
                Tenant = (object?)new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.MobileNumber,
                    tenant.PanCard,
                    tenant.MonthlyRent,
                    tenant.RentDueDay,
                    tenant.SecurityDeposit
                },
                Rent = (object?)new
                {
                    RentId = displayRent?.Id,
                    Year = displayRent?.Year ?? today.Year,
                    Month = displayRent?.Month ?? today.Month,
                    MonthLabel = displayRent is null
                        ? IndiaClock.MonthLabel(
                            tenant.RentStartYear,
                            tenant.RentStartMonth)
                        : IndiaClock.MonthLabel(displayRent.Year, displayRent.Month),
                    AmountDue = displayRent?.AmountDue ?? tenant.MonthlyRent,
                    AmountPaid = displayRent?.AmountPaid ?? 0m,
                    DueDate = displayRent?.DueDate,
                    Status = displayStatus?.Status ?? "Upcoming",
                    Timing = displayStatus?.Timing ?? "Rent not started yet",
                    DaysUntilDue = displayStatus?.DaysUntilDue ?? 0,
                    IsDueSoon = displayStatus?.IsDueSoon ?? false,
                    PreviousOutstanding = previousOutstanding,
                    TotalPayable = totalPayable
                }
            };
        });

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetShop(int id)
    {
        var shop = await _db.Shops
            .Where(s => s.Id == id && s.IsActive)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync();

        if (shop is null)
        {
            return ApiResults.Missing("Shop not found.");
        }

        return Ok(new
        {
            shop.Id,
            shop.Name,
            IsOccupied = shop.Tenant is { IsActive: true },
            Tenant = shop.Tenant is { IsActive: true }
                ? new
                {
                    shop.Tenant.Id,
                    shop.Tenant.Name,
                    shop.Tenant.MobileNumber,
                    shop.Tenant.PanCard,
                    shop.Tenant.MonthlyRent,
                    shop.Tenant.RentDueDay,
                    shop.Tenant.SecurityDeposit
                }
                : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateShop([FromBody] ShopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResults.Invalid("Shop name is required.");
        }

        var name = request.Name.Trim();

        // Only active shops block the name.
        var exists = await _db.Shops
            .AnyAsync(s => s.IsActive && s.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return ApiResults.Duplicate(
                $"A shop named \"{name}\" already exists. Please use a different name.");
        }

        var shop = new Shop { Name = name, IsActive = true };

        _db.Shops.Add(shop);

        await _db.SaveChangesAsync();

        return Created($"/api/shops/{shop.Id}", new
        {
            shop.Id,
            shop.Name,
            IsOccupied = false,
            Tenant = (object?)null
        });
    }

    // Works for occupied shops too. Renaming does not touch the tenant or
    // any rent record.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateShop(int id, [FromBody] ShopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResults.Invalid("Shop name is required.");
        }

        var shop = await _db.Shops
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (shop is null)
        {
            return ApiResults.Missing("Shop not found.");
        }

        var name = request.Name.Trim();

        var exists = await _db.Shops
            .AnyAsync(s =>
                s.Id != id &&
                s.IsActive &&
                s.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return ApiResults.Duplicate(
                $"A shop named \"{name}\" already exists. Please use a different name.");
        }

        shop.Name = name;

        await _db.SaveChangesAsync();

        return Ok(new { shop.Id, shop.Name });
    }

    // Soft delete: the row stays so rent history remains readable, but the
    // name is freed for reuse.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteShop(int id)
    {
        var shop = await _db.Shops
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (shop is null)
        {
            return ApiResults.Missing("Shop not found.");
        }

        if (shop.Tenant is { IsActive: true })
        {
            return ApiResults.Blocked(
                $"{shop.Name} still has {shop.Tenant.Name} assigned. Remove the tenant first.");
        }

        shop.IsActive = false;

        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public class ShopRequest
{
    public string Name { get; set; } = string.Empty;
}
