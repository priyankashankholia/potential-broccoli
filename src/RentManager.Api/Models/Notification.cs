namespace RentManager.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public int? RentId { get; set; }

    public Rent? Rent { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }
}