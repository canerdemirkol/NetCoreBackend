namespace NetCoreBackend.NArchitecture.Core.Payments.Billing;

/// <summary>The price of moving to a dearer edition before the period ends: the difference per period, for the time left. Pure.</summary>
public static class Proration
{
    public static Money UpgradePrice(Money currentPeriodPrice, Money newPeriodPrice, TimeSpan remaining, TimeSpan periodLength)
    {
        if (newPeriodPrice < currentPeriodPrice)
            throw new ArgumentException("A cheaper edition is a downgrade, not an upgrade.", nameof(newPeriodPrice));
        if (periodLength <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(periodLength), periodLength, "A period has a length.");
        if (remaining <= TimeSpan.Zero)
            return new Money(0, newPeriodPrice.Currency);

        Money difference = newPeriodPrice - currentPeriodPrice;
        decimal share = (decimal)remaining.Ticks / periodLength.Ticks;
        return Money.Of(difference.Amount * share, difference.Currency);
    }
}
