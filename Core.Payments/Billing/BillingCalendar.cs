namespace NetCoreBackend.NArchitecture.Core.Payments.Billing;

/// <summary>
/// Calendar periods, pure. A month ends on the anchor day of the next month, or on that month's last day when it is shorter (31 January →
/// 28/29 February → 31 March: the anchor is kept, so periods do not drift). A year ends on the same day next year. Days are counted in the
/// given time zone (the host's), the time of day is kept.
/// </summary>
public static class BillingCalendar
{
    private const int FirstDay = 1;
    private const int LastPossibleDay = 31;

    /// <summary>The anchor day a subscription keeps from its first period's start, in the time zone's calendar.</summary>
    public static int AnchorDayOf(DateTime startUtc, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTimeFromUtc(Utc(startUtc), timeZone).Day;

    public static DateTime EndOf(DateTime startUtc, BillingPeriod period, int anchorDay, TimeZoneInfo timeZone)
    {
        if (anchorDay is < FirstDay or > LastPossibleDay)
            throw new ArgumentOutOfRangeException(nameof(anchorDay), anchorDay, "An anchor day is a day of a month (1–31).");

        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(Utc(startUtc), timeZone);
        DateTime month = new DateTime(local.Year, local.Month, 1).AddMonths(period == BillingPeriod.Monthly ? 1 : 12);
        int day = Math.Min(anchorDay, DateTime.DaysInMonth(month.Year, month.Month));
        var endLocal = new DateTime(month.Year, month.Month, day, local.Hour, local.Minute, local.Second, DateTimeKind.Unspecified).AddTicks(local.Ticks % TimeSpan.TicksPerSecond);
        return TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
}
