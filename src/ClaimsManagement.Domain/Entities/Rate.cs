using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class Rate : TenantEntity
{
    public Guid RateTypeId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string Location { get; set; } = string.Empty; // City, Country, or specific location
    public DateTimeOffset? EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public RateType RateType { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
