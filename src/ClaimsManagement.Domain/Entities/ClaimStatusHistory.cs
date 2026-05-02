using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class ClaimStatusHistory : TenantEntity
{
    public Guid ClaimId { get; set; }
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string? Reason { get; set; }

    public Claim Claim { get; set; } = null!;
}
