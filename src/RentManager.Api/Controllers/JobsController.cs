using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Services;

namespace RentManager.Api.Controllers;

// Called by an external scheduler rather than run in-process. On the free
// App Service tier the app is unloaded after a few minutes idle, so a
// BackgroundService cannot be relied on to fire at 9am. The scheduler's
// request both wakes the app and runs the work.
[AllowAnonymous]
[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly RentGenerationService _generation;
    private readonly RentReminderService _reminders;
    private readonly NotificationDeliveryService _delivery;
    private readonly PushService _push;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        RentManagerDbContext db,
        RentGenerationService generation,
        RentReminderService reminders,
        NotificationDeliveryService delivery,
        PushService push,
        IConfiguration configuration,
        ILogger<JobsController> logger)
    {
        _db = db;
        _generation = generation;
        _reminders = reminders;
        _delivery = delivery;
        _push = push;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("daily")]
    public async Task<IActionResult> RunDaily(
        [FromHeader(Name = "X-Job-Key")] string? jobKey,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["Jobs:Key"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            _logger.LogError("Jobs__Key is not configured; refusing to run.");

            return StatusCode(500);
        }

        // Fixed-length comparison so the response time gives nothing away.
        if (!CryptographicEquals(jobKey, expected))
        {
            _logger.LogWarning("Daily job called with a bad key.");

            return Unauthorized();
        }

        // Order matters. Rents must exist before reminders can be raised
        // against them, and notifications must exist before delivery.
        var rents = await _generation.EnsureRentsUpToCurrentMonthAsync(
            cancellationToken: cancellationToken);

        var reminders = await _reminders.GenerateRemindersAsync(cancellationToken);

        var delivered = await _delivery.ProcessPendingNotificationsAsync(
            cancellationToken);

        // One push a day, and only when there is something to act on. A
        // notification that says "nothing to do" trains him to ignore them.
        var pending = await _db.Notifications
            .Where(n => n.Status == "Pending")
            .Select(n => n.TenantId)
            .Distinct()
            .CountAsync(cancellationToken);

        var pushed = 0;

        if (pending > 0)
        {
            pushed = await _push.SendToAllAsync(
                "Narera Complex",
                pending == 1
                    ? "1 shop needs chasing today"
                    : $"{pending} shops need chasing today",
                "/",
                cancellationToken);
        }

        _logger.LogInformation(
            "Daily job: {Rents} rents, {Reminders} reminders, {Delivered} delivered, {Pushed} pushed.",
            rents,
            reminders,
            delivered,
            pushed);

        return Ok(new { rents, reminders, delivered, pushed });
    }

    // A plain == on secrets leaks length and prefix through timing.
    private static bool CryptographicEquals(string? a, string b)
    {
        if (a is null)
        {
            return false;
        }

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
    }
}