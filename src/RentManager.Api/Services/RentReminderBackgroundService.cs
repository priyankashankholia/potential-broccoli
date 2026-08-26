namespace RentManager.Api.Services;

public class RentReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RentReminderBackgroundService> _logger;

    public RentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RentReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Rent reminder background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var reminderService =
                    scope.ServiceProvider
                        .GetRequiredService<RentReminderService>();

                var created =
                    await reminderService.GenerateRemindersAsync(
                        stoppingToken);

                _logger.LogInformation(
                    "Rent reminder check completed. Created {Count} notifications.",
                    created);
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
                    "Error while generating rent reminders.");
            }

            await Task.Delay(
                TimeSpan.FromHours(24),
                stoppingToken);
        }

        _logger.LogInformation(
            "Rent reminder background service stopped.");
    }
}