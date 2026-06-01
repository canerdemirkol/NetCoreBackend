namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Exceptions;

public class TenantNotActiveException : Exception
{
    public TenantNotActiveException(string identifier)
        : base($"Tenant '{identifier}' is not active.") { }
}
