#!/usr/bin/env bash
# Creates the push notification backend files.
# Run from the repo root: bash push-setup.sh
set -e

API="src/RentManager.Api"

if [ ! -d "$API" ]; then
  echo "Run this from the repo root (the folder holding src/ and rent-manager-web/)."
  exit 1
fi

cat > "$API/Models/PushSubscription.cs" << 'EOF'
namespace RentManager.Api.Models;

// One row per browser install, not per user. The landlord installing the
// PWA on his phone and his tablet produces two rows, and both get notified.
public class PushSubscription
{
    public int Id { get; set; }

    // The push service URL the browser gave us. Unique per install.
    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    public int UserId { get; set; }

    public AppUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Push services return 404 or 410 once a subscription is dead. Other
    // errors are counted instead, so a transient outage does not silently
    // unsubscribe a device.
    public int FailureCount { get; set; }

    public DateTime? LastSentAt { get; set; }
}
EOF
echo "  ok    Models/PushSubscription.cs"

cat > "$API/Services/PushService.cs" << 'EOF'
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using WebPush;

namespace RentManager.Api.Services;

public class PushService
{
    private const int MaxFailuresBeforeDropping = 3;

    private readonly RentManagerDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PushService> _logger;

    public PushService(
        RentManagerDbContext db,
        IConfiguration configuration,
        ILogger<PushService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Push:PublicKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Push:PrivateKey"]);

    public async Task<int> SendToAllAsync(
        string title,
        string body,
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Push keys are not configured; skipping send.");

            return 0;
        }

        var subscriptions = await _db.PushSubscriptions
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var vapid = new VapidDetails(
            _configuration["Push:Subject"] ?? "mailto:admin@example.com",
            _configuration["Push:PublicKey"],
            _configuration["Push:PrivateKey"]);

        var client = new WebPushClient();

        // No rent figures or tenant names. A notification sits on the lock
        // screen where anyone nearby can read it.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            body,
            url
        });

        var sent = 0;
        var dead = new List<Models.PushSubscription>();

        foreach (var subscription in subscriptions)
        {
            try
            {
                await client.SendNotificationAsync(
                    new WebPush.PushSubscription(
                        subscription.Endpoint,
                        subscription.P256dh,
                        subscription.Auth),
                    payload,
                    vapid,
                    cancellationToken);

                subscription.FailureCount = 0;
                subscription.LastSentAt = DateTime.UtcNow;

                sent++;
            }
            catch (WebPushException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // The push service says this subscription is finished:
                // uninstalled app, cleared site data, or similar.
                dead.Add(subscription);
            }
            catch (Exception ex)
            {
                subscription.FailureCount++;

                if (subscription.FailureCount >= MaxFailuresBeforeDropping)
                {
                    dead.Add(subscription);
                }

                _logger.LogWarning(
                    ex,
                    "Push failed for subscription {Id} (failure {Count}).",
                    subscription.Id,
                    subscription.FailureCount);
            }
        }

        if (dead.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(dead);

            _logger.LogInformation("Removed {Count} dead subscriptions.", dead.Count);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return sent;
    }
}
EOF
echo "  ok    Services/PushService.cs"

cat > "$API/Controllers/PushController.cs" << 'EOF'
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
    // design; serving it from config keeps the two ends in step.
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

        // Re-subscribing from the same install updates rather than
        // duplicating.
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
EOF
echo "  ok    Controllers/PushController.cs"

# --- DbContext -----------------------------------------------------------
CTX="$API/Data/RentManagerDbContext.cs"

if grep -q "PushSubscriptions" "$CTX"; then
  echo "  skip  RentManagerDbContext.cs (already registered)"
else
  python3 - << 'PYEOF'
import pathlib
p = pathlib.Path("src/RentManager.Api/Data/RentManagerDbContext.cs")
s = p.read_text()

s = s.replace(
    "    public DbSet<AppUser> Users => Set<AppUser>();",
    "    public DbSet<AppUser> Users => Set<AppUser>();\n"
    "    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();",
    1)

s = s.replace(
    "    protected override void OnModelCreating(ModelBuilder modelBuilder)\n    {\n",
    "    protected override void OnModelCreating(ModelBuilder modelBuilder)\n    {\n"
    "        // One subscription per browser install.\n"
    "        modelBuilder.Entity<PushSubscription>()\n"
    "            .HasIndex(s => s.Endpoint)\n"
    "            .IsUnique();\n\n",
    1)

p.write_text(s)
PYEOF
  echo "  ok    RentManagerDbContext.cs"
fi

# --- Program.cs ----------------------------------------------------------
if grep -q "AddScoped<PushService>" "$API/Program.cs"; then
  echo "  skip  Program.cs (already registered)"
else
  python3 - << 'PYEOF'
import pathlib, re, sys
p = pathlib.Path("src/RentManager.Api/Program.cs")
s = p.read_text()

m = re.search(r"builder\.Services\.AddScoped<\w+Service>\(\);", s)
if not m:
    sys.exit("Could not find an AddScoped service registration in Program.cs.")

s = s[:m.end()] + "\nbuilder.Services.AddScoped<PushService>();" + s[m.end():]
p.write_text(s)
PYEOF
  echo "  ok    Program.cs"
fi

echo
echo "Next:"
echo "  cd src/RentManager.Api"
echo "  source ../../dev-env.sh"
echo "  dotnet ef migrations add AddPushSubscriptions"
echo "  dotnet ef database update"
echo "  dotnet build"