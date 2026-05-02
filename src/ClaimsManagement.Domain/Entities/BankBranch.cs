using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class BankBranch : TenantEntity
{
    public Guid BankId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Branch code
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public Bank Bank { get; set; } = null!;
    public ICollection<UserBankAccount> Accounts { get; set; } = new List<UserBankAccount>();
}
