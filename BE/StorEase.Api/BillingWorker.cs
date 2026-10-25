using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.CheckIns;

namespace StorEase.Api;

/// <summary>
/// Runs the billing job every hour. The job is idempotent per day, so the hourly tick only matters for the first
/// run after midnight or after a restart.
/// </summary>
public class BillingWorker(IServiceScopeFactory scopes, ILogger<BillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IBillingCycleService>()
                    .RunAsync(VietnamTime.Today, stoppingToken);
                if (result.IsSuccess)
                    logger.LogInformation("Billing run: {@Run}", result.Value);
                else
                    logger.LogWarning("Billing run failed: {Error}", result.Error.Message);
                // Visits nobody checked out end at closing time; hourly is close enough because each ends at its closing.
                var closed = await scope.ServiceProvider.GetRequiredService<ICheckInService>()
                    .CloseAtClosingTimeAsync(DateTime.UtcNow, stoppingToken);
                if (closed > 0)
                    logger.LogInformation("Closed {Count} visits at closing time", closed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Billing run crashed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
