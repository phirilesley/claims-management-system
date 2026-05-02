namespace ClaimsManagement.Api.Contracts;

public record CreatePaymentBatchRequest(
    IReadOnlyList<Guid> ClaimIds,
    Guid CurrencyId,
    string Description);

public record PaymentBatchSummaryResponse(
    Guid Id,
    string Reference,
    string Status,
    decimal TotalAmount,
    string CurrencyCode,
    int PaymentCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc);
