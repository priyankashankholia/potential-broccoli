namespace RentManager.Api.Models;

public class Tenant
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? PanCard { get; set; }

    public decimal MonthlyRent { get; set; }

    public int RentDueDay { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public DateTime? LeaseStartDate { get; set; }

    public DateTime? LeaseEndDate { get; set; }

    public int ShopId { get; set; }

    public Shop? Shop { get; set; }

    public ICollection<Rent> Rents { get; set; } = new List<Rent>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
