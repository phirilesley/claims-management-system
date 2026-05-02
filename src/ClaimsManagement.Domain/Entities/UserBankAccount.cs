using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class UserBankAccount : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid BankId { get; set; }
    public Guid? BranchId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty; // "Checking", "Savings", "Business"
    public string Currency { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? RoutingNumber { get; set; }
    public string? SwiftCode { get; set; }
    public string? Iban { get; set; }
    public string? Notes { get; set; }

    public Bank Bank { get; set; } = null!;
    public BankBranch? Branch { get; set; } = null!;
}
