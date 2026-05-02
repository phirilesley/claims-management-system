using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class ClaimPayment : TenantEntity
{
    public Guid ClaimId { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public decimal Amount { get; set; }
    public decimal? OriginalAmount { get; set; }
    public Guid? OriginalCurrencyId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public string? PaymentReference { get; set; }

    public Claim Claim { get; set; } = null!;
    public PaymentBatch? PaymentBatch { get; set; }
}
