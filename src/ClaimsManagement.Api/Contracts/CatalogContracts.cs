namespace ClaimsManagement.Api.Contracts;

public sealed record ClaimTypeOptionResponse(Guid Id, string Code, string Name);

public sealed record CurrencyOptionResponse(Guid Id, string Code, string Name);
