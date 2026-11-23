namespace NetCoreBackend.NArchitecture.Core.Payments.Masking;

/// <summary>
/// Field names, compared ignoring case and separators ("api_key" = "apiKey"). Secrets are starred; personal data leaves the body for a
/// separate store (the log is kept long; personal data may have to be erased sooner).
/// </summary>
public sealed record PaymentMaskingRules(IReadOnlySet<string> SecretFields, IReadOnlySet<string> PersonalFields)
{
    public static readonly PaymentMaskingRules Default = new(
        Normalized("apiKey", "secretKey", "secret", "password", "hash", "signature", "token", "paytrToken", "merchantKey", "merchantSalt",
            "authorization", "conversationData", "cardToken", "cvc", "cvv", "expireMonth", "expireYear", "cardHolderName"),
        Normalized("name", "surname", "fullName", "email", "phone", "gsmNumber", "address", "registrationAddress", "identityNumber", "userName",
            "userAddress", "userPhone", "contactName"));

    /// <summary>The default rules with a provider's own field names added.</summary>
    public PaymentMaskingRules With(IEnumerable<string>? secretFields = null, IEnumerable<string>? personalFields = null) => new(
        SecretFields.Concat(Normalized([.. secretFields ?? []])).ToHashSet(),
        PersonalFields.Concat(Normalized([.. personalFields ?? []])).ToHashSet());

    internal static HashSet<string> Normalized(params string[] names) => names.Select(Normalize).ToHashSet();

    internal static string Normalize(string name) => new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
