using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class PaymentMethod : TenantEntity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // "Bank", "Cash", "MobileMoney", "Check"
    public string Name { get; set; } = string.Empty; // e.g., "Personal Bank Account", "Office Cash"
    public string? BankAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? Details { get; set; } // Additional details like "Cash on hand", "Mobile provider"

    public UserBankAccount? BankAccount { get; set; } = null!;
}
