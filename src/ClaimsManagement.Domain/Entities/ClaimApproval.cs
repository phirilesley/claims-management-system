using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class ClaimApproval : TenantEntity
{
    public Guid ClaimId { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public Guid? ApproverUserId { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comment { get; set; }
    public DateTimeOffset? ActionAtUtc { get; set; }

    public Claim Claim { get; set; } = null!;
}
