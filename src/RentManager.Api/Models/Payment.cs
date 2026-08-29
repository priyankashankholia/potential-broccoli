namespace RentManager.Api.Models;

public class Payment
{
    public int Id { get; set; }

    public int RentId { get; set; }

    public Rent Rent { get; set; } = null!;

    public decimal Amount { get; set; }

    // The India calendar date the money changed hands.
    public DateOnly PaymentDate { get; set; }

    public string PaymentMode { get; set; } = "Cash";

    public string? Note { get; set; }

    // Audit stamp for when the row was written.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
