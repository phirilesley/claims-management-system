using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class ClaimLine : TenantEntity
{
    public Guid ClaimId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Category { get; set; }
    public decimal? MileageKm { get; set; }
    public decimal? PerDiemDays { get; set; }
    public string? MetadataJson { get; set; }

    public Claim Claim { get; set; } = null!;
}
