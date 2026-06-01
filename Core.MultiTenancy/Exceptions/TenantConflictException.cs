namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Exceptions;

// Thrown when the request carries conflicting tenant signals (e.g. JWT tenant_id != X-Tenant-ID header).
// Surface this as HTTP 400 — the client is sending contradictory information.
public class TenantConflictException : Exception
{
    public TenantConflictException(string message) : base(message) { }
}
