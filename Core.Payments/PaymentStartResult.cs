namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// The provider's answer to a start: its token and the payment page (an address to open or embed, or a form's HTML), or an error. Provider
/// errors are results, not exceptions.
/// </summary>
public sealed record PaymentStartResult(
    bool Succeeded, string? ProviderToken, string? PaymentPageUrl, string? PaymentPageHtml, string? ErrorCode, string? ErrorMessage)
{
    public static PaymentStartResult Failure(string code, string? message) => new(false, null, null, null, code, message);
}
