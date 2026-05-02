namespace ClaimsManagement.Api.Contracts;

public sealed record CreateClaimLineRequest(
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    string? Category,
    decimal? MileageKm,
    decimal? PerDiemDays,
    string? MetadataJson);

public sealed record CreateClaimRequest(
    Guid ClaimTypeId,
    string Title,
    Guid CurrencyId,
    bool Submit,
    string? DynamicDataJson,
    string? BankDetailsJson,
    IReadOnlyList<CreateClaimLineRequest> Lines);

public sealed record ClaimLineResponse(
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal LineTotal,
    string? Category);

public sealed record ClaimSummaryResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Status,
    string CurrencyCode,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc);
