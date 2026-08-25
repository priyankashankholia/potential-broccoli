using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/rents")]
public class RentsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public RentsController(RentManagerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetRents()
    {
        var rents = await _db.Rents
            .Include(r => r.Tenant)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                TenantName = r.Tenant.Name,
                r.Year,
                r.Month,
                r.AmountDue,
                r.AmountPaid,
                Remaining = r.AmountDue - r.AmountPaid,
                r.DueDate,
                r.IsSettled
            })
            .ToListAsync();

        return Ok(rents);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRent(int id)
    {
        var rent = await _db.Rents
            .Include(r => r.Tenant)
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                TenantName = r.Tenant.Name,
                r.Year,
                r.Month,
                r.AmountDue,
                r.AmountPaid,
                Remaining = r.AmountDue - r.AmountPaid,
                r.DueDate,
                r.IsSettled
            })
            .FirstOrDefaultAsync();

        if (rent is null)
        {
            return NotFound();
        }

        return Ok(rent);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRent(
        [FromBody] CreateRentRequest request)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId);

        if (tenant is null)
        {
            return BadRequest("Tenant not found.");
        }

        if (request.Month < 1 || request.Month > 12)
        {
            return BadRequest("Month must be between 1 and 12.");
        }

        if (request.Year < 2000)
        {
            return BadRequest("Invalid year.");
        }

        var existingRent = await _db.Rents
            .FirstOrDefaultAsync(r =>
                r.TenantId == request.TenantId &&
                r.Year == request.Year &&
                r.Month == request.Month);

        if (existingRent is not null)
        {
            return Conflict(
                "Rent already exists for this tenant and month.");
        }

        var rent = new Rent
        {
            TenantId = request.TenantId,
            Year = request.Year,
            Month = request.Month,
            AmountDue = tenant.MonthlyRent,
            AmountPaid = 0,
            DueDate = request.DueDate,
            IsSettled = false
        };

        _db.Rents.Add(rent);

        await _db.SaveChangesAsync();

        return Created(
            $"/api/rents/{rent.Id}",
            new
            {
                rent.Id,
                rent.TenantId,
                rent.Year,
                rent.Month,
                rent.AmountDue,
                rent.AmountPaid,
                Remaining = rent.AmountDue,
                rent.DueDate,
                rent.IsSettled
            });
    }

    [HttpPost("generate/{year:int}/{month:int}")]
    public async Task<IActionResult> GenerateMonthlyRent(
        int year,
        int month)
    {
        if (month < 1 || month > 12)
        {
            return BadRequest("Month must be between 1 and 12.");
        }

        var tenants = await _db.Tenants
            .ToListAsync();

        var created = 0;

        foreach (var tenant in tenants)
        {
            var exists = await _db.Rents
                .AnyAsync(r =>
                    r.TenantId == tenant.Id &&
                    r.Year == year &&
                    r.Month == month);

            if (exists)
            {
                continue;
            }

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

            _db.Rents.Add(new Rent
            {
                TenantId = tenant.Id,
                Year = year,
                Month = month,
                AmountDue = tenant.MonthlyRent,
                AmountPaid = 0,
                DueDate = dueDate,
                IsSettled = false
            });

            created++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Year = year,
            Month = month,
            Created = created
        });
    }
}

public class CreateRentRequest
{
    public int TenantId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public DateTime DueDate { get; set; }
}