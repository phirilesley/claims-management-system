using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class RateType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty; // e.g., "per night", "per day", "per meal"
    public decimal DefaultAmount { get; set; }
    public Guid CurrencyId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresReceipt { get; set; } = true;
    public decimal MaxDailyAmount { get; set; }
    public int MaxOccurrencesPerDay { get; set; } = 1;
    public string? Category { get; set; } // e.g., "Accommodation", "Meals", "Transport", "Entertainment"

    public Currency Currency { get; set; } = null!;
    public ICollection<Rate> Rates { get; set; } = new List<Rate>();
}
