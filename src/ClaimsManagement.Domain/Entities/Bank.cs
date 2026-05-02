using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class Bank : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Bank code/SWIFT
    public string Country { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty; // Primary currency
    public bool IsActive { get; set; } = true;
    public string? Website { get; set; }
    public string? ContactInfo { get; set; }

    public ICollection<BankBranch> Branches { get; set; } = new List<BankBranch>();
    public ICollection<UserBankAccount> UserAccounts { get; set; } = new List<UserBankAccount>();
}
