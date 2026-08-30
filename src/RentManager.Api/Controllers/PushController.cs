using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;

namespace RentManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly IConfiguration _configuration;

    public PushController(RentManagerDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // The browser needs this before it can subscribe. It is public by
    // design, so serving it from config rather than hardcoding it in the
    // Angular bundle just keeps the two in step.
    [AllowAnonymous]
    [HttpGet("key")]
    public IActionResult GetPublicKey()
    {
        var key = _configuration["Push:PublicKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound(new { message = "Push is not configured." });
        }

        return Ok(new { publicKey = key });
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(
        [FromBody] SubscribeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) ||
            string.IsNullOrWhiteSpace(request.P256dh) ||
            string.IsNullOrWhiteSpace(request.Auth))
        {
            return BadRequest(new { message = "Incomplete subscription." });
        }

        var username = User.Identity?.Name;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        // Re-subscribing from the same install should update, not duplicate.
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(
                s => s.Endpoint == request.Endpoint,
                cancellationToken);

        if (existing is not null)
        {
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserId = user.Id;
            existing.FailureCount = 0;
        }
        else
        {
            _db.PushSubscriptions.Add(new Models.PushSubscription
            {
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserId = user.Id
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] UnsubscribeRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(
                s => s.Endpoint == request.Endpoint,
                cancellationToken);

        if (existing is not null)
        {
            _db.PushSubscriptions.Remove(existing);

            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}

public class SubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;
}

public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}