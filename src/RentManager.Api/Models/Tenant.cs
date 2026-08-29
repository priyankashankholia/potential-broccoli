namespace RentManager.Api.Models;

public class Tenant
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? PanCard { get; set; }

    // Applies to rents generated from here on. Changing it never rewrites
    // a month that already exists, so a rent change takes effect from the
    // next generated month.
    public decimal MonthlyRent { get; set; }

    public int RentDueDay { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public DateTime? LeaseStartDate { get; set; }

    public DateTime? LeaseEndDate { get; set; }

    // First month this tenant owes rent for. Set when the tenant is added,
    // based on whether this month's due day had already passed. Generation
    // never creates anything before it.
    public int RentStartYear { get; set; }

    public int RentStartMonth { get; set; }

    // Only the first month. Set when the landlord starts rent in a month
    // whose normal due day has already gone by, so that month becomes
    // collectable from the joining date instead of being born overdue.
    public DateOnly? FirstDueDate { get; set; }

    public int? ShopId { get; set; }

    public Shop? Shop { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Rent> Rents { get; set; } = new List<Rent>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
