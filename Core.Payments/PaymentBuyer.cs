namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// The payer as the provider must see them (fraud checks, the payment page); personal data, masked out of the log body. The address is the
/// billing address. A provider that needs a field the caller left empty refuses the start with an error result.
/// </summary>
public sealed record PaymentBuyer(
    string Id, string FirstName, string LastName, string Email, string? Phone, string? Address, string? City, string? Country, string? PostalCode,
    string? IdentityNumber, string Ip)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
