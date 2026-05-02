using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class AuditLog : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? ChangesJson { get; set; }
}
