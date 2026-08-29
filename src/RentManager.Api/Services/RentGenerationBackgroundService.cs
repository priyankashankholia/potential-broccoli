namespace RentManager.Api.Services;

// Runs rent generation at startup and then hourly, so a new month's rent
// appears on its own even if nobody opens the app on the 1st.
public class RentGenerationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RentGenerationBackgroundService> _logger;

    public RentGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RentGenerationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rent generation service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var generator = scope.ServiceProvider
                    .GetRequiredService<RentGenerationService>();

                await generator.EnsureRentsUpToCurrentMonthAsync(
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rent generation failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Rent generation service stopped.");
    }
}
