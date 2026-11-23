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
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IBillingCycleService>()
                    .RunAsync(VietnamTime.Today, stoppingToken);
                if (result.IsSuccess)
                    logger.LogInformation("Billing run: {@Run}", result.Value);
                else
                    logger.LogWarning("Billing run failed: {Error}", result.Error.Message);
                // Missed handover appointments expire (deposit forfeited); bookings whose unit stays rented are offered another.
                var policy = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Repository<PricingPolicy>().Query()
                    .Where(p => p.Status == PricingPolicyStatus.Active).Select(p => (int?)p.EndReminderDays).FirstOrDefaultAsync(stoppingToken);
                var (expired, proposed) = await scope.ServiceProvider.GetRequiredService<IReservationService>()
                    .RunDailyAsync(VietnamTime.Today, policy ?? 10, stoppingToken);
                if (expired + proposed > 0)
                    logger.LogInformation("Bookings: {Expired} expired, {Proposed} offered another unit", expired, proposed);
                // Rentals near or past their end date: reminder, overstay status and its growing fee.
                var ends = await scope.ServiceProvider.GetRequiredService<IRentalEndService>().RunAsync(VietnamTime.Today, stoppingToken);
                if (ends.Reminded + ends.Overstayed + ends.FeesUpdated > 0)
                    logger.LogInformation("Rental ends: {@Run}", ends);
                // Weekly timetables keep their shifts created two weeks ahead.
                var shiftsMade = await scope.ServiceProvider.GetRequiredService<IShiftService>().ExtendTimetablesAsync(stoppingToken);
                if (shiftsMade > 0)
                    logger.LogInformation("Created {Count} shifts from weekly timetables", shiftsMade);
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
