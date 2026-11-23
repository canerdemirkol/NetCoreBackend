namespace Core.Test.Payments;

/// <summary>
/// A test that calls iyzico's sandbox (never the live API): it runs only when IYZICO_SANDBOX_API_KEY and IYZICO_SANDBOX_SECRET_KEY are set,
/// so builds without the keys skip it instead of failing.
/// </summary>
public sealed class IyzicoSandboxFactAttribute : FactAttribute
{
    public const string ApiKeyVariable = "IYZICO_SANDBOX_API_KEY";
    public const string SecretKeyVariable = "IYZICO_SANDBOX_SECRET_KEY";

    public IyzicoSandboxFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ApiKeyVariable)) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SecretKeyVariable)))
            Skip = $"{ApiKeyVariable} / {SecretKeyVariable} are not set.";
    }
}
