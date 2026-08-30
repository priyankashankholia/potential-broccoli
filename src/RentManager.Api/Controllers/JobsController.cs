using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly RentGenerationService _generation;
    private readonly RentReminderService _reminders;
    private readonly NotificationDeliveryService _delivery;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        RentGenerationService generation,
        RentReminderService reminders,
        NotificationDeliveryService delivery,
        IConfiguration configuration,
        ILogger<JobsController> logger)
    {
        _generation = generation;
        _reminders = reminders;
        _delivery = delivery;
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

        _logger.LogInformation(
            "Daily job: {Rents} rents, {Reminders} reminders, {Delivered} delivered.",
            rents,
            reminders,
            delivered);

        return Ok(new { rents, reminders, delivered });
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