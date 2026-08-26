using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;

namespace RentManager.Api.Services;

public class NotificationDeliveryService
{
    private readonly RentManagerDbContext _db;
    private readonly ILogger<NotificationDeliveryService> _logger;

    public NotificationDeliveryService(
        RentManagerDbContext db,
        ILogger<NotificationDeliveryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> ProcessPendingNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .Include(n => n.Tenant)
            .Where(n => n.Status == "Pending")
            .OrderBy(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processed = 0;

        foreach (var notification in notifications)
        {
            try
            {
                if (notification.Tenant is null)
                {
                    notification.Status = "Failed";
                    continue;
                }

                var mobileNumber =
                    notification.Tenant.MobileNumber;

                if (string.IsNullOrWhiteSpace(mobileNumber))
                {
                    notification.Status = "Failed";
                    continue;
                }

                /*
                 * Provider integration will be added here.
                 *
                 * For now we only log the message.
                 * This keeps the application functional
                 * without requiring WhatsApp/SMS credentials.
                 */

                _logger.LogInformation(
                    "Notification ready for delivery. " +
                    "Tenant: {TenantId}, Channel: {Channel}, " +
                    "Mobile: {MobileNumber}, Message: {Message}",
                    notification.TenantId,
                    notification.Channel,
                    mobileNumber,
                    notification.Message);

                notification.Status = "Sent";
notification.SentAt = DateTime.UtcNow;

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process notification {NotificationId}.",
                    notification.Id);

                notification.Status = "Failed";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return processed;
    }
}