namespace StorEase.Application.Common;

/// <summary>
/// Request timestamps without an offset ("2026-10-12T09:00") are the facility's wall clock, which is
/// always Vietnam (UTC+7, no DST). <c>DateTime.ToUniversalTime()</c> would instead read them in the
/// server's own zone, so the stored UTC value would depend on where the API happens to run.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value - Offset, DateTimeKind.Utc),
    };
}
