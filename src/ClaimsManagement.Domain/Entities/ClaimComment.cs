using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class ClaimComment : TenantEntity
{
    public Guid ClaimId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;

    public Claim Claim { get; set; } = null!;
}
