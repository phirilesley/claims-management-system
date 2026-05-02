using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class Claim : TenantEntity
{
    public Guid ClaimTypeId { get; set; }
    public Guid EmployeeId { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public Guid CurrencyId { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    /// <summary>Dynamic field answers / mileage metadata (JSONB).</summary>
    public string? DynamicDataJson { get; set; }

    /// <summary>Encrypted-at-rest in production; MVP stores JSON.</summary>
    public string? BankDetailsJson { get; set; }

    public int CurrentWorkflowStep { get; set; }

    public ClaimType ClaimType { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public Currency Currency { get; set; } = null!;

    public ICollection<ClaimLine> Lines { get; set; } = new List<ClaimLine>();
    public ICollection<ClaimAttachment> Attachments { get; set; } = new List<ClaimAttachment>();
    public ICollection<ClaimApproval> Approvals { get; set; } = new List<ClaimApproval>();
    public ICollection<ClaimComment> Comments { get; set; } = new List<ClaimComment>();
    public ICollection<ClaimStatusHistory> StatusHistory { get; set; } = new List<ClaimStatusHistory>();
    public ICollection<ClaimPayment> Payments { get; set; } = new List<ClaimPayment>();
}
