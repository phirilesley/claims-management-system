namespace ClaimsManagement.Api.Contracts;

// Banks
public record CreateBankRequest(
    string Name,
    string Code,
    string Country,
    string Currency,
    string? Website,
    string? ContactInfo);

public record BankResponse(
    Guid Id,
    string Code,
    string Name,
    string Country,
    string Currency,
    string? Website,
    string? ContactInfo);

// Bank Branches
public record CreateBankBranchRequest(
    string Name,
    string Code,
    string Address,
    string City,
    string Country,
    string? PhoneNumber,
    string? Email);

public record BankBranchResponse(
    Guid Id,
    string Code,
    string Name,
    string Address,
    string City,
    string Country,
    string? PhoneNumber,
    string? Email);

// User Bank Accounts
public record CreateUserBankAccountRequest(
    Guid BankId,
    Guid? BranchId,
    string AccountNumber,
    string AccountName,
    string AccountType,
    string Currency,
    string? RoutingNumber,
    string? SwiftCode,
    string? Iban,
    string? Notes);

public record UserBankAccountResponse(
    Guid Id,
    Guid BankId,
    string BankName,
    Guid? BranchId,
    string? BranchName,
    string AccountNumber,
    string AccountName,
    string AccountType,
    string Currency,
    bool IsDefault,
    bool IsActive,
    string? RoutingNumber,
    string? SwiftCode,
    string? Iban,
    string? Notes);

// Payment Methods
public record CreatePaymentMethodRequest(
    string Type,
    string Name,
    string Currency,
    Guid? BankAccountId,
    string? Details);

public record PaymentMethodResponse(
    Guid Id,
    string Type,
    string Name,
    string Currency,
    bool IsDefault,
    bool IsActive,
    string? Details,
    Guid? BankAccountId,
    string? BankName,
    string? BankAccountName);
