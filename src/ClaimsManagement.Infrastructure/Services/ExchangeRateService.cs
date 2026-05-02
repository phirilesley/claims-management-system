using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClaimsManagement.Contracts;

public interface IExchangeRateService
{
    Task<IReadOnlyList<ExchangeRate>> GetExchangeRatesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ExchangeRate> CreateExchangeRateAsync(Guid tenantId, CreateExchangeRateRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<ExchangeRate> UpdateExchangeRateAsync(Guid tenantId, Guid rateId, UpdateExchangeRateRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<decimal> ConvertCurrencyAsync(Guid tenantId, decimal amount, Guid fromCurrencyId, Guid toCurrencyId, DateTimeOffset date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Currency>> GetAvailableCurrenciesAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteExchangeRateAsync(Guid tenantId, Guid rateId, Guid deletedByUserId, CancellationToken cancellationToken = default);
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly ApplicationDbContext _db;

    public ExchangeRateService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetExchangeRatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.ExchangeRates
            .AsNoTracking()
            .Include(er => er.FromCurrency)
            .Include(er => er.ToCurrency)
            .Where(er => er.TenantId == tenantId)
            .OrderByDescending(er => er.EffectiveFromUtc)
            .ThenBy(er => er.FromCurrency.Code)
            .ThenBy(er => er.ToCurrency.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExchangeRate> CreateExchangeRateAsync(Guid tenantId, CreateExchangeRateRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate currencies exist
        var fromCurrency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.Id == request.FromCurrencyId && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Source currency not found or inactive.");

        var toCurrency = await _db.Currencies
            .FirstOrDefaultAsync(c => c.Id == request.ToCurrencyId && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Target currency not found or inactive.");

        if (fromCurrency.Id == toCurrency.Id)
            throw new InvalidOperationException("Cannot create exchange rate for the same currency.");

        // Check for overlapping rates
        var overlapping = await _db.ExchangeRates
            .AnyAsync(er => er.TenantId == tenantId &&
                           er.FromCurrencyId == request.FromCurrencyId &&
                           er.ToCurrencyId == request.ToCurrencyId &&
                           er.IsActive &&
                           er.EffectiveFromUtc <= request.EffectiveToUtc &&
                           (!er.EffectiveToUtc.HasValue || er.EffectiveToUtc >= request.EffectiveFromUtc), cancellationToken);

        if (overlapping)
            throw new InvalidOperationException("An active exchange rate already exists for this period and currency pair.");

        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromCurrencyId = request.FromCurrencyId,
            ToCurrencyId = request.ToCurrencyId,
            Rate = request.Rate,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            IsActive = true,
            Source = request.Source,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.ExchangeRates.Add(exchangeRate);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(ExchangeRate),
            EntityId = exchangeRate.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.FromCurrencyId,
                request.ToCurrencyId,
                request.Rate,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Source
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(exchangeRate).Reference(er => er.FromCurrency).LoadAsync(cancellationToken);
        await _db.Entry(exchangeRate).Reference(er => er.ToCurrency).LoadAsync(cancellationToken);
        return exchangeRate;
    }

    public async Task<ExchangeRate> UpdateExchangeRateAsync(Guid tenantId, Guid rateId, UpdateExchangeRateRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var exchangeRate = await _db.ExchangeRates
            .FirstOrDefaultAsync(er => er.Id == rateId && er.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Exchange rate not found.");

        var changes = new Dictionary<string, object>();

        if (exchangeRate.Rate != request.Rate)
        {
            changes["Rate"] = new { Old = exchangeRate.Rate, New = request.Rate };
            exchangeRate.Rate = request.Rate;
        }

        if (exchangeRate.EffectiveFromUtc != request.EffectiveFromUtc)
        {
            changes["EffectiveFromUtc"] = new { Old = exchangeRate.EffectiveFromUtc, New = request.EffectiveFromUtc };
            exchangeRate.EffectiveFromUtc = request.EffectiveFromUtc;
        }

        if (exchangeRate.EffectiveToUtc != request.EffectiveToUtc)
        {
            changes["EffectiveToUtc"] = new { Old = exchangeRate.EffectiveToUtc, New = request.EffectiveToUtc };
            exchangeRate.EffectiveToUtc = request.EffectiveToUtc;
        }

        if (exchangeRate.IsActive != request.IsActive)
        {
            changes["IsActive"] = new { Old = exchangeRate.IsActive, New = request.IsActive };
            exchangeRate.IsActive = request.IsActive;
        }

        if (exchangeRate.Source != request.Source)
        {
            changes["Source"] = new { Old = exchangeRate.Source, New = request.Source };
            exchangeRate.Source = request.Source;
        }

        exchangeRate.ModifiedAtUtc = DateTimeOffset.UtcNow;

        if (changes.Any())
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityType = nameof(ExchangeRate),
                EntityId = exchangeRate.Id,
                Action = "Update",
                UserId = updatedByUserId,
                ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(exchangeRate).Reference(er => er.FromCurrency).LoadAsync(cancellationToken);
        await _db.Entry(exchangeRate).Reference(er => er.ToCurrency).LoadAsync(cancellationToken);
        return exchangeRate;
    }

    public async Task<decimal> ConvertCurrencyAsync(Guid tenantId, decimal amount, Guid fromCurrencyId, Guid toCurrencyId, DateTimeOffset date, CancellationToken cancellationToken = default)
    {
        if (fromCurrencyId == toCurrencyId)
            return amount;

        // Try to find direct exchange rate
        var directRate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(er => er.TenantId == tenantId &&
                       er.FromCurrencyId == fromCurrencyId &&
                       er.ToCurrencyId == toCurrencyId &&
                       er.IsActive &&
                       er.EffectiveFromUtc <= date &&
                       (!er.EffectiveToUtc.HasValue || er.EffectiveToUtc >= date))
            .OrderByDescending(er => er.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (directRate != null)
            return amount * directRate.Rate;

        // Try to find inverse exchange rate
        var inverseRate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(er => er.TenantId == tenantId &&
                       er.FromCurrencyId == toCurrencyId &&
                       er.ToCurrencyId == fromCurrencyId &&
                       er.IsActive &&
                       er.EffectiveFromUtc <= date &&
                       (!er.EffectiveToUtc.HasValue || er.EffectiveToUtc >= date))
            .OrderByDescending(er => er.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (inverseRate != null && inverseRate.Rate != 0)
            return amount / inverseRate.Rate;

        // If no exchange rate found, throw exception
        var fromCurrency = await _db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == fromCurrencyId, cancellationToken);
        var toCurrency = await _db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == toCurrencyId, cancellationToken);

        throw new InvalidOperationException($"No exchange rate found from {fromCurrency?.Code} to {toCurrency?.Code} for date {date:yyyy-MM-dd}.");
    }

    public async Task<IReadOnlyList<Currency>> GetAvailableCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Currencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteExchangeRateAsync(Guid tenantId, Guid rateId, Guid deletedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var exchangeRate = await _db.ExchangeRates
            .FirstOrDefaultAsync(er => er.Id == rateId && er.TenantId == tenantId, cancellationToken);

        if (exchangeRate == null)
            return false;

        // Soft delete by setting IsActive to false
        exchangeRate.IsActive = false;
        exchangeRate.ModifiedAtUtc = DateTimeOffset.UtcNow;

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(ExchangeRate),
            EntityId = exchangeRate.Id,
            Action = "Delete",
            UserId = deletedByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                FromCurrencyId = exchangeRate.FromCurrencyId,
                ToCurrencyId = exchangeRate.ToCurrencyId,
                Rate = exchangeRate.Rate,
                EffectiveFromUtc = exchangeRate.EffectiveFromUtc
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return true;
    }
}
