namespace RentManager.Api.Models;

public class Rent
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal AmountDue { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime DueDate { get; set; }

    public bool IsSettled { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
