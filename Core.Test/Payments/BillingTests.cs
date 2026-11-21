using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Billing;

namespace Core.Test.Payments;

/// <summary>
/// Calendar periods (the same day next month or year; a short month ends on its last day, the anchor day keeps later months from
/// drifting) and the prorated price of an upgrade (the difference for the time left, in minor units).
/// </summary>
public sealed class BillingTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // 10:00 in Istanbul (UTC+3) on the given day.
    private static DateTime IstanbulMorning(int year, int month, int day) => new(year, month, day, 7, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2026, 1, 31, 31, 2026, 2, 28)]
    [InlineData(2028, 1, 31, 31, 2028, 2, 29)]
    [InlineData(2026, 2, 28, 31, 2026, 3, 31)]
    [InlineData(2026, 3, 15, 15, 2026, 4, 15)]
    [InlineData(2026, 12, 20, 20, 2027, 1, 20)]
    public void AMonth_EndsOnTheAnchorDay_OrTheMonthsLastDay(int y, int m, int d, int anchor, int ey, int em, int ed)
    {
        DateTime end = BillingCalendar.EndOf(IstanbulMorning(y, m, d), BillingPeriod.Monthly, anchor, Istanbul);

        Assert.Equal(IstanbulMorning(ey, em, ed), end);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
    }

    [Theory]
    [InlineData(2026, 11, 21, 21, 2027, 11, 21)]
    [InlineData(2028, 2, 29, 29, 2029, 2, 28)]
    public void AYear_EndsOnTheSameDayNextYear(int y, int m, int d, int anchor, int ey, int em, int ed)
    {
        Assert.Equal(IstanbulMorning(ey, em, ed), BillingCalendar.EndOf(IstanbulMorning(y, m, d), BillingPeriod.Annual, anchor, Istanbul));
    }

    [Fact]
    public void TheDayIsTheHostTimeZonesDay()
    {
        // 31 January 22:30 UTC is already 1 February in Istanbul: the period runs to 1 March there.
        var lateEvening = new DateTime(2026, 1, 31, 22, 30, 0, DateTimeKind.Utc);

        DateTime end = BillingCalendar.EndOf(lateEvening, BillingPeriod.Monthly, BillingCalendar.AnchorDayOf(lateEvening, Istanbul), Istanbul);

        Assert.Equal(1, BillingCalendar.AnchorDayOf(lateEvening, Istanbul));
        Assert.Equal(new DateTime(2026, 2, 28, 22, 30, 0, DateTimeKind.Utc), end);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void AnAnchorDayOutsideAMonth_IsRefused(int anchor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BillingCalendar.EndOf(IstanbulMorning(2026, 1, 1), BillingPeriod.Monthly, anchor, Istanbul));
    }

    [Fact]
    public void AnUpgrade_CostsTheDifference_ForTheTimeLeft()
    {
        TimeSpan period = TimeSpan.FromDays(30);

        Money half = Proration.UpgradePrice(Money.Of(100m), Money.Of(200m), TimeSpan.FromDays(15), period);
        Money twoPeriods = Proration.UpgradePrice(Money.Of(100m), Money.Of(200m), TimeSpan.FromDays(60), period);
        Money third = Proration.UpgradePrice(Money.Of(100m), Money.Of(200m), TimeSpan.FromDays(10), period);

        Assert.Equal(Money.Of(50m), half);
        Assert.Equal(Money.Of(200m), twoPeriods);
        Assert.Equal(Money.Of(33.33m), third);
    }

    [Fact]
    public void NoTimeLeft_CostsNothing()
    {
        Assert.Equal(Money.Of(0m), Proration.UpgradePrice(Money.Of(100m), Money.Of(200m), TimeSpan.Zero, TimeSpan.FromDays(30)));
        Assert.Equal(Money.Of(0m), Proration.UpgradePrice(Money.Of(100m), Money.Of(200m), TimeSpan.FromDays(-1), TimeSpan.FromDays(30)));
    }

    [Fact]
    public void ACheaperEdition_IsNotAnUpgrade()
    {
        Assert.Throws<ArgumentException>(() => Proration.UpgradePrice(Money.Of(200m), Money.Of(100m), TimeSpan.FromDays(10), TimeSpan.FromDays(30)));
    }
}
