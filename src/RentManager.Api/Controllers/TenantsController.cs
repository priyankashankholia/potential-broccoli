using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public TenantsController(RentManagerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _db.Tenants
            .Include(t => t.Shop)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.MobileNumber,
                t.PanCard,
                t.MonthlyRent,
                t.RentDueDay,
                t.SecurityDeposit,
                Shop = t.Shop == null
                    ? null
                    : new
                    {
                        t.Shop.Id,
                        t.Shop.Name
                    }
            })
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTenant(int id)
    {
        var tenant = await _db.Tenants
            .Include(t => t.Shop)
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.MobileNumber,
                t.PanCard,
                t.MonthlyRent,
                t.RentDueDay,
                t.SecurityDeposit,
                Shop = t.Shop == null
                    ? null
                    : new
                    {
                        t.Shop.Id,
                        t.Shop.Name
                    }
            })
            .FirstOrDefaultAsync();

        if (tenant == null)
        {
            return NotFound();
        }

        return Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            return BadRequest("Mobile number is required.");
        }

        if (request.MonthlyRent <= 0)
        {
            return BadRequest("Monthly rent must be greater than zero.");
        }

        if (request.RentDueDay < 1 || request.RentDueDay > 31)
        {
            return BadRequest(
                "Rent due day must be between 1 and 31.");
        }

        var shop = await _db.Shops
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == request.ShopId);

        if (shop == null)
        {
            return BadRequest("Shop not found.");
        }

        if (shop.Tenant != null)
        {
            return BadRequest(
                "This shop already has a tenant.");
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            MobileNumber = request.MobileNumber.Trim(),
            PanCard = string.IsNullOrWhiteSpace(request.PanCard)
                ? null
                : request.PanCard.Trim(),
            MonthlyRent = request.MonthlyRent,
            RentDueDay = request.RentDueDay,
            SecurityDeposit = request.SecurityDeposit,
            ShopId = request.ShopId
        };

        _db.Tenants.Add(tenant);

        await _db.SaveChangesAsync();

        // Automatically create the current month's rent.
        var now = DateTime.UtcNow;

        var year = now.Year;
        var month = now.Month;

        var dueDay = Math.Min(
            tenant.RentDueDay,
            DateTime.DaysInMonth(year, month));

        var dueDate = new DateTime(
            year,
            month,
            dueDay,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var rent = new Rent
        {
            TenantId = tenant.Id,
            Year = year,
            Month = month,
            AmountDue = tenant.MonthlyRent,
            AmountPaid = 0,
            DueDate = dueDate,
            IsSettled = false
        };

        _db.Rents.Add(rent);

        await _db.SaveChangesAsync();

        return Created(
            $"/api/tenants/{tenant.Id}",
            new
            {
                Tenant = new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.MobileNumber,
                    tenant.MonthlyRent,
                    tenant.RentDueDay,
                    tenant.ShopId
                },

                Rent = new
                {
                    rent.Id,
                    rent.Year,
                    rent.Month,
                    rent.AmountDue,
                    rent.AmountPaid,
                    Remaining = rent.AmountDue,
                    rent.DueDate,
                    rent.IsSettled
                }
            });
    }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? PanCard { get; set; }

    public decimal MonthlyRent { get; set; }

    public int RentDueDay { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public int ShopId { get; set; }
}