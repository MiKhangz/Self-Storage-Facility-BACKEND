using StorEase.Application.Features.Contracts;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.CheckIns;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Staff;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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
            // Each step has its own scope and error handling: one failing step must not stop the others
            // (a crash in the timetable step once kept visits open past closing time).
            await Step("Billing run", async services =>
            {
                var result = await services.GetRequiredService<IBillingCycleService>().RunAsync(VietnamTime.Today, stoppingToken);
                if (result.IsSuccess)
                    logger.LogInformation("Billing run: {@Run}", result.Value);
                else
                    logger.LogWarning("Billing run failed: {Error}", result.Error.Message);
            });
            // Missed handover appointments expire (deposit forfeited); bookings whose unit stays rented are offered another.
            await Step("Bookings", async services =>
            {
                var policy = await services.GetRequiredService<IUnitOfWork>().Repository<PricingPolicy>().Query()
                    .Where(p => p.Status == PricingPolicyStatus.Active).Select(p => (int?)p.EndReminderDays).FirstOrDefaultAsync(stoppingToken);
                var (expired, proposed) = await services.GetRequiredService<IReservationService>()
                    .RunDailyAsync(VietnamTime.Today, policy ?? 10, stoppingToken);
                if (expired + proposed > 0)
                    logger.LogInformation("Bookings: {Expired} expired, {Proposed} offered another unit", expired, proposed);
            });
            // Rentals near or past their end date: reminder, overstay status and its growing fee.
            await Step("Rental ends", async services =>
            {
                var ends = await services.GetRequiredService<IRentalEndService>().RunAsync(VietnamTime.Today, stoppingToken);
                if (ends.Reminded + ends.Overstayed + ends.FeesUpdated > 0)
                    logger.LogInformation("Rental ends: {@Run}", ends);
            });
            // Weekly timetables keep their shifts created two weeks ahead.
            await Step("Timetables", async services =>
            {
                var shiftsMade = await services.GetRequiredService<IShiftService>().ExtendTimetablesAsync(stoppingToken);
                if (shiftsMade > 0)
                    logger.LogInformation("Created {Count} shifts from weekly timetables", shiftsMade);
            });
            // Visits nobody checked out end at closing time; hourly is close enough because each ends at its closing.
            await Step("Visit closing", async services =>
            {
                var closed = await services.GetRequiredService<ICheckInService>().CloseAtClosingTimeAsync(DateTime.UtcNow, stoppingToken);
                if (closed > 0)
                    logger.LogInformation("Closed {Count} visits at closing time", closed);
            });
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Runs one step in a fresh scope, so a failed save cannot leave its database context broken for the next step.</summary>
    private async Task Step(string name, Func<IServiceProvider, Task> run)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await run(scope.ServiceProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Step} crashed", name);
        }
    }
}
