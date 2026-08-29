using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Services;

// Owns two things the controllers used to do themselves:
//
//  1. Working out a rent's paid amount. The old code did AmountPaid += x
//     on create and -= x on delete, which drifted whenever an edge case
//     was missed. It is now always re-derived as SUM(payments).
//
//  2. Deciding which months are collectable and allocating a payment
//     oldest month first, so carried-forward balances clear in order.
public class RentLedgerService
{
    private readonly RentManagerDbContext _db;

    public RentLedgerService(RentManagerDbContext db)
    {
        _db = db;
    }

    // Call after any payment create, update or delete. The caller saves.
    public async Task RecalculateAsync(
        IReadOnlyCollection<int> rentIds,
        CancellationToken cancellationToken = default)
    {
        if (rentIds.Count == 0)
        {
            return;
        }

        var rents = await _db.Rents
            .Where(r => rentIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var totals = await _db.Payments
            .Where(p => rentIds.Contains(p.RentId))
            .GroupBy(p => p.RentId)
            .Select(g => new { RentId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        var lookup = totals.ToDictionary(t => t.RentId, t => t.Paid);

        foreach (var rent in rents)
        {
            rent.AmountPaid = lookup.TryGetValue(rent.Id, out var paid) ? paid : 0m;
            rent.IsSettled = rent.AmountPaid >= rent.AmountDue;
        }
    }

    // Months this tenant can pay right now, oldest first. Eligibility comes
    // from RentStatusCalculator so the payment screen and the payment API
    // can never disagree about what is collectable.
    public async Task<List<Rent>> GetPayableRentsAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var today = IndiaClock.Today();

        var rents = await _db.Rents
            .Where(r => r.TenantId == tenantId && r.AmountPaid < r.AmountDue)
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .ToListAsync(cancellationToken);

        return rents
            .Where(r => RentStatusCalculator.IsPayable(r, today))
            .ToList();
    }

    // The single "Total to be Paid" figure shown to the landlord.
    public async Task<decimal> GetTotalPayableAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var payable = await GetPayableRentsAsync(tenantId, cancellationToken);

        return payable.Sum(r => Math.Max(0m, r.AmountDue - r.AmountPaid));
    }
}
