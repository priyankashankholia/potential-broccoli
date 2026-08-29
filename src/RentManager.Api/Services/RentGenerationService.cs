using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Services;

// Creates each active tenant's rent row as soon as a month starts. There
// is no Generate Rent button any more.
//
// Runs on startup, hourly, and right after a tenant is created. Safe to
// call repeatedly because of the existence check plus the unique index on
// (TenantId, Year, Month).
public class RentGenerationService
{
    private readonly RentManagerDbContext _db;
    private readonly ILogger<RentGenerationService> _logger;

    public RentGenerationService(
        RentManagerDbContext db,
        ILogger<RentGenerationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> EnsureRentsUpToCurrentMonthAsync(
        int? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var today = IndiaClock.Today();
        var currentKey = IndiaClock.MonthKey(today.Year, today.Month);

        var tenantsQuery = _db.Tenants.Where(t => t.IsActive);

        if (tenantId.HasValue)
        {
            tenantsQuery = tenantsQuery.Where(t => t.Id == tenantId.Value);
        }

        var tenants = await tenantsQuery.ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            return 0;
        }

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var existing = await _db.Rents
            .Where(r => tenantIds.Contains(r.TenantId))
            .Select(r => new { r.TenantId, r.Year, r.Month })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(r => (r.TenantId, Key: IndiaClock.MonthKey(r.Year, r.Month)))
            .ToHashSet();

        var created = 0;

        foreach (var tenant in tenants)
        {
            // RentStartYear/Month is set when the tenant is added, based
            // on which month's due date had not yet passed. Nothing is
            // ever generated before it.
            var startKey = IndiaClock.MonthKey(
                tenant.RentStartYear,
                tenant.RentStartMonth);

            if (startKey > currentKey)
            {
                continue;
            }

            // Catch up on every month from the start month to now, so a
            // gap of two months creates both and both carry forward.
            for (var key = startKey; key <= currentKey; key++)
            {
                if (existingKeys.Contains((tenant.Id, key)))
                {
                    continue;
                }

                var (year, month) = IndiaClock.MonthFromKey(key);

                _db.Rents.Add(new Rent
                {
                    TenantId = tenant.Id,
                    Year = year,
                    Month = month,
                    AmountDue = tenant.MonthlyRent,
                    AmountPaid = 0m,
                    // Only the very first month can carry an overridden due
                    // date. Every later month uses the normal due day.
                    DueDate = key == startKey && tenant.FirstDueDate.HasValue
                        ? tenant.FirstDueDate.Value
                        : IndiaClock.DueDateFor(year, month, tenant.RentDueDay),
                    IsSettled = false
                });

                created++;
            }
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated {Count} rent record(s) up to {Year}-{Month:00}.",
                created,
                today.Year,
                today.Month);
        }

        return created;
    }
}
