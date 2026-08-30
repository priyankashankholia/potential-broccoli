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

        var subscriptions = await _db.PushSubscriptions.ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var vapid = new VapidDetails(
            _configuration["Push:Subject"] ?? "mailto:admin@example.com",
            _configuration["Push:PublicKey"],
            _configuration["Push:PrivateKey"]);

        var client = new WebPushClient();

        // No rent figures or names. This sits on a lock screen.
        var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url });

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
                dead.Add(subscription);
            }
            catch (Exception ex)
            {
                subscription.FailureCount++;

                if (subscription.FailureCount >= MaxFailuresBeforeDropping)
                {
                    dead.Add(subscription);
                }

                _logger.LogWarning(ex, "Push failed for subscription {Id}.", subscription.Id);
            }
        }

        if (dead.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(dead);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return sent;
    }
}
