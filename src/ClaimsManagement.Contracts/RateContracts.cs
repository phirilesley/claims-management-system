namespace ClaimsManagement.Api.Contracts;

// Rate Types
public record CreateRateTypeRequest(
    string Name,
    string Code,
    string Description,
    string Unit,
    decimal DefaultAmount,
    Guid CurrencyId,
    bool RequiresReceipt,
    decimal MaxDailyAmount,
    int MaxOccurrencesPerDay,
    string? Category);

public record UpdateRateTypeRequest(
    string Name,
    string Description,
    string Unit,
    decimal DefaultAmount,
    bool RequiresReceipt,
    decimal MaxDailyAmount,
    int MaxOccurrencesPerDay,
    string? Category);

public record RateTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Unit,
    decimal DefaultAmount,
    string CurrencyCode,
    bool RequiresReceipt,
    decimal MaxDailyAmount,
    int MaxOccurrencesPerDay,
    string? Category);

// Rates
public record CreateRateRequest(
    Guid RateTypeId,
    Guid EmployeeId,
    decimal Amount,
    string Location,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    string? Notes);

public record UpdateRateRequest(
    decimal Amount,
    string Location,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    bool IsActive,
    string? Notes);

public record RateResponse(
    Guid Id,
    Guid RateTypeId,
    string RateTypeName,
    string RateTypeCode,
    Guid EmployeeId,
    string EmployeeName,
    decimal Amount,
    string Location,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    bool IsActive,
    string? Notes);
