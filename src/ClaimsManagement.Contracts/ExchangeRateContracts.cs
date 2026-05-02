namespace ClaimsManagement.Api.Contracts;

public record CreateExchangeRateRequest(
    Guid FromCurrencyId,
    Guid ToCurrencyId,
    decimal Rate,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    string? Source);

public record UpdateExchangeRateRequest(
    decimal Rate,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    bool IsActive,
    string? Source);

public record ExchangeRateResponse(
    Guid Id,
    Guid FromCurrencyId,
    string FromCurrencyCode,
    string FromCurrencyName,
    Guid ToCurrencyId,
    string ToCurrencyCode,
    string ToCurrencyName,
    decimal Rate,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    bool IsActive,
    string? Source);

public record ConvertCurrencyRequest(
    decimal Amount,
    Guid FromCurrencyId,
    Guid ToCurrencyId,
    DateTimeOffset Date);

public record CurrencyOptionResponse(
    Guid Id,
    string Code,
    string Name);
