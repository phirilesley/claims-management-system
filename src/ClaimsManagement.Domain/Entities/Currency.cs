using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

/// <summary>Shared catalog currency row (USD, ZWG/ZiG, etc.).</summary>
public class Currency : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ExchangeRate> RatesFrom { get; set; } = new List<ExchangeRate>();
    public ICollection<ExchangeRate> RatesTo { get; set; } = new List<ExchangeRate>();
}
