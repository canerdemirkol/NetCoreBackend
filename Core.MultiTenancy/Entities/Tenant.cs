using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Entities;

public class Tenant : Entity<Guid>
{
    public string Name { get; set; }
    public string Identifier { get; set; }
    public string? Domain { get; set; }
    public bool IsActive { get; set; }
    public TenantPlanType PlanType { get; set; }
    // BCP 47 locale code used as fallback when the client sends no Accept-Language header. e.g. "tr", "de", "en"
    public string? DefaultLocale { get; set; }

    public Tenant()
    {
        Name = string.Empty;
        Identifier = string.Empty;
        IsActive = true;
        PlanType = TenantPlanType.Free;
    }

    public Tenant(Guid id, string name, string identifier, string? domain = null,
        TenantPlanType planType = TenantPlanType.Free, string? defaultLocale = null)
        : base(id)
    {
        Name = name;
        Identifier = identifier;
        Domain = domain;
        IsActive = true;
        PlanType = planType;
        DefaultLocale = defaultLocale;
    }
}

public enum TenantPlanType
{
    Free,
    Basic,
    Pro,
    Enterprise
}
