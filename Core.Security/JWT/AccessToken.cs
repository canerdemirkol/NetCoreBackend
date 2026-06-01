namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

public class AccessToken
{
    public string Token { get; set; }
    public DateTime ExpirationDate { get; set; }

    // Parameterless constructor leaves ExpirationDate at DateTime.MinValue (the default).
    // Callers using this constructor MUST set ExpirationDate before treating the token as valid;
    // otherwise the token appears already expired by every standard JWT lifetime check.
    public AccessToken()
    {
        Token = string.Empty;
        ExpirationDate = DateTime.MinValue;
    }

    public AccessToken(string token, DateTime expirationDate)
    {
        Token = token;
        ExpirationDate = expirationDate;
    }
}
