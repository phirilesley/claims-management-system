using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class PaymentBatch : TenantEntity
{
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CurrencyId { get; set; }
    public PaymentBatchStatus Status { get; set; } = PaymentBatchStatus.Draft;
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public DateTimeOffset? ExportedAtUtc { get; set; }

    public Currency Currency { get; set; } = null!;
    public ICollection<ClaimPayment> Payments { get; set; } = new List<ClaimPayment>();
}
