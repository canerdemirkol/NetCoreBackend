namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging;

public class LogDetail
{
    public string FullName { get; set; }
    public string MethodName { get; set; }
    public string User { get; set; }
    public string? TenantId { get; set; }
    public List<LogParameter> Parameters { get; set; }

    public LogDetail()
    {
        FullName = string.Empty;
        MethodName = string.Empty;
        User = string.Empty;
        Parameters = [];
    }

    public LogDetail(string fullName, string methodName, string user, List<LogParameter> parameters, string? tenantId = null)
    {
        FullName = fullName;
        MethodName = methodName;
        User = user;
        TenantId = tenantId;
        Parameters = parameters;
    }
}
