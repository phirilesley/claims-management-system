using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class ClaimType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>JSON schema / field definitions for dynamic claim forms (JSONB).</summary>
    public string? FormSchemaJson { get; set; }

    /// <summary>Workflow steps for this claim type (JSON array).</summary>
    public string[] WorkflowSteps { get; set; } = Array.Empty<string>();

    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}
