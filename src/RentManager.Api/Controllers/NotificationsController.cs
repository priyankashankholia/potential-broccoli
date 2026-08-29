using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;

namespace RentManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly RentManagerDbContext _db;

    public NotificationsController(RentManagerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _db.Notifications
            .Include(n => n.Tenant)
            .Include(n => n.Rent)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.TenantId,
                TenantName = n.Tenant!.Name,
                n.RentId,
                n.Type,
                n.Channel,
                n.Message,
                n.Status,
                n.CreatedAt,
                n.SentAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingNotifications()
    {
        var notifications = await _db.Notifications
            .Where(n => n.Status == "Pending")
            .Include(n => n.Tenant)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.TenantId,
                TenantName = n.Tenant!.Name,
                n.RentId,
                n.Type,
                n.Channel,
                n.Message,
                n.Status,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }
}