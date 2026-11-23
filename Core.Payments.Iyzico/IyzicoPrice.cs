using System.Globalization;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// Prices as iyzico writes and signs them: a dot and no trailing zeros ("50.00" signs as "50", "10.50" as "10.5"). Answers carry numbers or
/// strings; both are read.
/// </summary>
public static class IyzicoPrice
{
    private const string NoTrailingZeros = "0.############";

    public static string Format(decimal amount) => amount.ToString(NoTrailingZeros, CultureInfo.InvariantCulture);

    public static decimal? Read(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) => value,
        _ => null,
    };
}
