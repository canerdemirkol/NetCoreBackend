using NetCoreBackend.NArchitecture.Core.Payments;

namespace Core.Test.Payments;

/// <summary>Money is counted in minor units; VAT always adds up; an order's status moves only by the table.</summary>
public class MoneyTests
{
    [Theory]
    [InlineData(10.005, 1001)]   // half a kuruş rounds away from zero
    [InlineData(10.004, 1000)]
    [InlineData(-10.005, -1001)]
    public void AnAmount_IsKeptInMinorUnits(double amount, long minor)
    {
        Assert.Equal(minor, Money.Of((decimal)amount).Minor);
    }

    [Fact]
    public void DifferentCurrencies_DoNotAdd()
    {
        Assert.Throws<InvalidOperationException>(() => Money.Of(1m, "TRY") + Money.Of(1m, "USD"));
        Assert.Equal(Money.Of(3m), Money.Of(1m) + Money.Of(2m));
    }

    [Theory]
    [InlineData(120.00, 0.20, 100.00, 20.00)]
    [InlineData(99.99, 0.20, 83.33, 16.66)]
    [InlineData(0.01, 0.20, 0.01, 0.00)]
    public void AGrossPrice_SplitsIntoNetAndVat_ThatAddUp(double gross, double rate, double net, double vat)
    {
        VatSplit split = VatSplit.FromGross(Money.Of((decimal)gross), (decimal)rate);

        Assert.Equal(Money.Of((decimal)net), split.Net);
        Assert.Equal(Money.Of((decimal)vat), split.Vat);
        Assert.Equal(split.Total, split.Net + split.Vat);
        Assert.Equal(Money.Of((decimal)gross), split.Total);
    }

    [Theory]
    [InlineData(100.00, 0.20, 20.00, 120.00)]
    [InlineData(83.33, 0.20, 16.67, 100.00)]
    public void ANetPrice_GetsItsVat_ThatAddsUp(double net, double rate, double vat, double gross)
    {
        VatSplit split = VatSplit.FromNet(Money.Of((decimal)net), (decimal)rate);

        Assert.Equal(Money.Of((decimal)vat), split.Vat);
        Assert.Equal(Money.Of((decimal)gross), split.Total);
    }
}
