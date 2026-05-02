using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class ExchangeRate : TenantEntity
{
    public Guid FromCurrencyId { get; set; }
    public Guid ToCurrencyId { get; set; }
    public decimal Rate { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Source { get; set; } // e.g., "Central Bank", "XE.com", "Manual"

    public Currency FromCurrency { get; set; } = null!;
    public Currency ToCurrency { get; set; } = null!;
}
