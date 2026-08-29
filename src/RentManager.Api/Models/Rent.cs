namespace RentManager.Api.Models;

public class Rent
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public int Year { get; set; }

    public int Month { get; set; }

    // Frozen at generation time, so a later rent change does not rewrite
    // a month that already exists.
    public decimal AmountDue { get; set; }

    // Always re-derived as SUM(payments) by RentLedgerService, never
    // incremented in place.
    public decimal AmountPaid { get; set; }

    // Stored as a PostgreSQL date. No time, no timezone, no drift.
    public DateOnly DueDate { get; set; }

    public bool IsSettled { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
