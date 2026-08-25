using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/shops")]
public class ShopsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public ShopsController(RentManagerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetShops()
    {
        var shops = await _db.Shops
            .Include(s => s.Tenant)
            .Select(s => new
            {
                s.Id,
                s.Name,
                IsOccupied = s.Tenant != null,
                Tenant = s.Tenant == null
                    ? null
                    : new
                    {
                        s.Tenant.Id,
                        s.Tenant.Name,
                        s.Tenant.MobileNumber
                    }
            })
            .OrderBy(s => s.Id)
            .ToListAsync();

        return Ok(shops);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetShop(int id)
    {
        var shop = await _db.Shops
            .Include(s => s.Tenant)
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                IsOccupied = s.Tenant != null,
                Tenant = s.Tenant == null
                    ? null
                    : new
                    {
                        s.Tenant.Id,
                        s.Tenant.Name,
                        s.Tenant.MobileNumber
                    }
            })
            .FirstOrDefaultAsync();

        if (shop == null)
        {
            return NotFound();
        }

        return Ok(shop);
    }

    [HttpPost]
    public async Task<IActionResult> CreateShop(
        [FromBody] CreateShopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Shop name is required.");
        }

        var name = request.Name.Trim();

        var exists = await _db.Shops
            .AnyAsync(s => s.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return Conflict("A shop with this name already exists.");
        }

        var shop = new Shop
        {
            Name = name
        };

        _db.Shops.Add(shop);

        await _db.SaveChangesAsync();

        return Created(
            $"/api/shops/{shop.Id}",
            new
            {
                shop.Id,
                shop.Name,
                IsOccupied = false,
                Tenant = (object?)null
            });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateShop(
        int id,
        [FromBody] CreateShopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Shop name is required.");
        }

        var shop = await _db.Shops
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shop == null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();

        var exists = await _db.Shops
            .AnyAsync(s =>
                s.Id != id &&
                s.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return Conflict("A shop with this name already exists.");
        }

        shop.Name = name;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            shop.Id,
            shop.Name
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteShop(int id)
    {
        var shop = await _db.Shops
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shop == null)
        {
            return NotFound();
        }

        if (shop.Tenant != null)
        {
            return BadRequest(
                "Cannot delete a shop while a tenant is assigned.");
        }

        _db.Shops.Remove(shop);

        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateShopRequest
{
    public string Name { get; set; } = string.Empty;
}