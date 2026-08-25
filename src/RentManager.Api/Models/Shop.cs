namespace RentManager.Api.Models;

public class Shop
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsOccupied { get; set; }

    public Tenant? Tenant { get; set; }
}
