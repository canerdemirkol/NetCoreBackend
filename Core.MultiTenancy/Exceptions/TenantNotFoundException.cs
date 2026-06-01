namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Exceptions;

public class TenantNotFoundException : Exception
{
    public TenantNotFoundException(string identifier)
        : base($"Tenant '{identifier}' was not found.") { }
}
