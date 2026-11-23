namespace NetCoreBackend.NArchitecture.Core.Payments;

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
