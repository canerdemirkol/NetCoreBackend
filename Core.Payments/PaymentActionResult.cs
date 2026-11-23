namespace NetCoreBackend.NArchitecture.Core.Payments;

public sealed record PaymentActionResult(bool Succeeded, string? ProviderReference, string? ErrorCode, string? ErrorMessage)
{
    public static PaymentActionResult Failure(string code, string? message) => new(false, null, code, message);
}
