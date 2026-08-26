namespace RentManager.Api.Services;

public class NotificationDeliveryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDeliveryBackgroundService> _logger;

    public NotificationDeliveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDeliveryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Notification delivery background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var deliveryService =
                    scope.ServiceProvider
                        .GetRequiredService<NotificationDeliveryService>();

                var processed =
                    await deliveryService
                        .ProcessPendingNotificationsAsync(
                            stoppingToken);

                _logger.LogInformation(
                    "Notification delivery check completed. " +
                    "Processed {Count} notifications.",
                    processed);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while processing pending notifications.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);
        }

        _logger.LogInformation(
            "Notification delivery background service stopped.");
    }
}