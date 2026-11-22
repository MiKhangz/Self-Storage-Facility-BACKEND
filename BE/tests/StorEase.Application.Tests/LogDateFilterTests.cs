using FluentAssertions;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A day in a log filter is a Vietnam day: it starts at 17:00 UTC the day before.</summary>
public class LogDateFilterTests
{
    private static readonly DateOnly Day = new(2026, 11, 21);

    private static readonly DateTime[] Moments =
    [
        new(2026, 11, 20, 16, 59, 59, DateTimeKind.Utc), // 23:59:59 on the 20th
        new(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc),   // 00:00 on the 21st
        new(2026, 11, 21, 16, 59, 59, DateTimeKind.Utc), // 23:59:59 on the 21st
        new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc),   // 00:00 on the 22nd
    ];

    [Fact]
    public async Task The_activity_log_filters_by_the_Vietnam_day()
    {
        using var t = Create();
        foreach (var at in Moments)
            t.Db.Add(new ActivityLog { UserId = StaffId, Action = "POST Test", Result = ActivityResult.Success, CreatedAt = at });
        t.Db.SaveChanges();

        var found = await new ActivityLogService(t.Uow).ListAsync(null, null, null, null, true, Day, Day, 1, 20, default);

        found.Value!.Items.Select(l => l.CreatedAt).Should().BeEquivalentTo(Moments[1..3]);
    }

    [Fact]
    public async Task The_access_log_filters_by_the_Vietnam_day()
    {
        using var t = Create();
        var (_, contractId) = SeedReadyForHandover(t, signed: true);
        var code = new AccessCode { ContractId = contractId, CodeHash = "h", IssuedAt = Moments[0], ExpiresAt = Moments[3], IsActive = true };
        t.Db.Add(code);
        t.Db.SaveChanges();
        foreach (var at in Moments)
            t.Db.Add(new AccessLog { AccessCodeId = code.AccessCodeId, StorageUnitId = A101, AccessTime = at });
        t.Db.SaveChanges();

        var found = await new AccessLogService(t.Uow, Customer).ListMineAsync(Day, Day, 1, 20, default);

        found.Value!.Items.Select(l => l.AccessTime).Should().BeEquivalentTo(Moments[1..3]);
    }
}
