using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Domain.Common;

public abstract class TenantEntity : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
