namespace RentManager.Api.Models;

public class Shop
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Shops are soft-deleted so old rent history stays readable. Only
    // active names have to be unique, which is what lets a deleted shop
    // name be used again.
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
