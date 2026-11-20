namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// An amount in minor units (kuruş, cent) of one currency. Arithmetic never mixes currencies; decimals are rounded once, away from zero,
/// when an amount enters.
/// </summary>
public readonly record struct Money(long Minor, string Currency)
{
    public const string DefaultCurrency = "TRY";
    private const int MinorUnitsPerUnit = 100;

    public static Money Of(decimal amount, string currency = DefaultCurrency) =>
        new((long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero), currency);

    public decimal Amount => (decimal)Minor / MinorUnitsPerUnit;

    public static Money operator +(Money left, Money right) => new(checked(left.Minor + SameCurrency(left, right).Minor), left.Currency);

    public static Money operator -(Money left, Money right) => new(checked(left.Minor - SameCurrency(left, right).Minor), left.Currency);

    public static bool operator >(Money left, Money right) => left.Minor > SameCurrency(left, right).Minor;

    public static bool operator <(Money left, Money right) => left.Minor < SameCurrency(left, right).Minor;

    private static Money SameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? right
            : throw new InvalidOperationException($"Cannot combine {left.Currency} with {right.Currency}.");

    public override string ToString() => $"{Amount:0.00} {Currency}";
}

/// <summary>
/// A price's net amount, VAT and total, always adding up (net + VAT = total). The rate is a fraction (0.20 for 20%). One rounding: the part
/// computed from the other is rounded, the remaining one is the difference.
/// </summary>
public sealed record VatSplit(Money Net, Money Vat, Money Total)
{
    /// <summary>A VAT-inclusive price: net = total / (1 + rate).</summary>
    public static VatSplit FromGross(Money total, decimal rate)
    {
        Money net = Money.Of(total.Amount / (1 + rate), total.Currency);
        return new VatSplit(net, total - net, total);
    }

    /// <summary>A VAT-exclusive price: VAT = net × rate.</summary>
    public static VatSplit FromNet(Money net, decimal rate)
    {
        Money vat = Money.Of(net.Amount * rate, net.Currency);
        return new VatSplit(net, vat, net + vat);
    }
}
