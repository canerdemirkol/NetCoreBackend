using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>PayTR's user_basket: base64 of a JSON list of [name, unit price with a dot and two decimals, quantity] (dev.paytr.com samples).</summary>
public static class PayTrBasket
{
    private const int Quantity = 1;

    public static string Encode(string name, Money price)
    {
        string json = JsonSerializer.Serialize(new object[] { new object[] { name, price.Amount.ToString("0.00", CultureInfo.InvariantCulture), Quantity } });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
