using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClaimsManagement.Contracts;

public interface IRateManagementService
{
    Task<IReadOnlyList<RateType>> GetRateTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<RateType> CreateRateTypeAsync(Guid tenantId, CreateRateTypeRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<RateType> UpdateRateTypeAsync(Guid tenantId, Guid rateTypeId, UpdateRateTypeRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rate>> GetRatesAsync(Guid tenantId, Guid? employeeId = null, Guid? rateTypeId = null, CancellationToken cancellationToken = default);
    Task<Rate> CreateRateAsync(Guid tenantId, CreateRateRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<Rate> UpdateRateAsync(Guid tenantId, Guid rateId, UpdateRateRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<decimal> GetApplicableRateAsync(Guid tenantId, Guid rateTypeId, Guid employeeId, string location, DateTimeOffset date, CancellationToken cancellationToken = default);
}

public class RateManagementService : IRateManagementService
{
    private readonly ApplicationDbContext _db;

    public RateManagementService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RateType>> GetRateTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.RateTypes
            .AsNoTracking()
            .Include(rt => rt.Currency)
            .Where(rt => rt.TenantId == tenantId && rt.IsActive)
            .OrderBy(rt => rt.Category)
            .ThenBy(rt => rt.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<RateType> CreateRateTypeAsync(Guid tenantId, CreateRateTypeRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Check for duplicate code
        var existing = await _db.RateTypes
            .AnyAsync(rt => rt.TenantId == tenantId && rt.Code == request.Code, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A rate type with this code already exists.");

        var rateType = new RateType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            Unit = request.Unit,
            DefaultAmount = request.DefaultAmount,
            CurrencyId = request.CurrencyId,
            RequiresReceipt = request.RequiresReceipt,
            MaxDailyAmount = request.MaxDailyAmount,
            MaxOccurrencesPerDay = request.MaxOccurrencesPerDay,
            Category = request.Category,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.RateTypes.Add(rateType);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(RateType),
            EntityId = rateType.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.Name,
                request.Code,
                request.Description,
                request.Unit,
                request.DefaultAmount,
                request.CurrencyId,
                request.RequiresReceipt,
                request.MaxDailyAmount,
                request.MaxOccurrencesPerDay,
                request.Category
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(rateType).Reference(rt => rt.Currency).LoadAsync(cancellationToken);
        return rateType;
    }

    public async Task<RateType> UpdateRateTypeAsync(Guid tenantId, Guid rateTypeId, UpdateRateTypeRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var rateType = await _db.RateTypes
            .FirstOrDefaultAsync(rt => rt.Id == rateTypeId && rt.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Rate type not found.");

        var changes = new Dictionary<string, object>();

        if (rateType.Name != request.Name)
        {
            changes["Name"] = new { Old = rateType.Name, New = request.Name };
            rateType.Name = request.Name;
        }

        if (rateType.Description != request.Description)
        {
            changes["Description"] = new { Old = rateType.Description, New = request.Description };
            rateType.Description = request.Description;
        }

        if (rateType.Unit != request.Unit)
        {
            changes["Unit"] = new { Old = rateType.Unit, New = request.Unit };
            rateType.Unit = request.Unit;
        }

        if (rateType.DefaultAmount != request.DefaultAmount)
        {
            changes["DefaultAmount"] = new { Old = rateType.DefaultAmount, New = request.DefaultAmount };
            rateType.DefaultAmount = request.DefaultAmount;
        }

        if (rateType.RequiresReceipt != request.RequiresReceipt)
        {
            changes["RequiresReceipt"] = new { Old = rateType.RequiresReceipt, New = request.RequiresReceipt };
            rateType.RequiresReceipt = request.RequiresReceipt;
        }

        if (rateType.MaxDailyAmount != request.MaxDailyAmount)
        {
            changes["MaxDailyAmount"] = new { Old = rateType.MaxDailyAmount, New = request.MaxDailyAmount };
            rateType.MaxDailyAmount = request.MaxDailyAmount;
        }

        if (rateType.MaxOccurrencesPerDay != request.MaxOccurrencesPerDay)
        {
            changes["MaxOccurrencesPerDay"] = new { Old = rateType.MaxOccurrencesPerDay, New = request.MaxOccurrencesPerDay };
            rateType.MaxOccurrencesPerDay = request.MaxOccurrencesPerDay;
        }

        if (rateType.Category != request.Category)
        {
            changes["Category"] = new { Old = rateType.Category, New = request.Category };
            rateType.Category = request.Category;
        }

        rateType.ModifiedAtUtc = DateTimeOffset.UtcNow;

        if (changes.Any())
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityType = nameof(RateType),
                EntityId = rateType.Id,
                Action = "Update",
                UserId = updatedByUserId,
                ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(rateType).Reference(rt => rt.Currency).LoadAsync(cancellationToken);
        return rateType;
    }

    public async Task<IReadOnlyList<Rate>> GetRatesAsync(Guid tenantId, Guid? employeeId = null, Guid? rateTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Rates
            .AsNoTracking()
            .Include(r => r.RateType)
                .ThenInclude(rt => rt.Currency)
            .Include(r => r.Employee)
            .Where(r => r.TenantId == tenantId);

        if (employeeId.HasValue)
            query = query.Where(r => r.EmployeeId == employeeId.Value);

        if (rateTypeId.HasValue)
            query = query.Where(r => r.RateTypeId == rateTypeId.Value);

        return await query
            .OrderByDescending(r => r.EffectiveFromUtc)
            .ThenBy(r => r.RateType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Rate> CreateRateAsync(Guid tenantId, CreateRateRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate rate type exists
        var rateType = await _db.RateTypes
            .FirstOrDefaultAsync(rt => rt.Id == request.RateTypeId && rt.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Rate type not found.");

        // Validate employee exists
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Employee not found.");

        var rate = new Rate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RateTypeId = request.RateTypeId,
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            Location = request.Location,
            EffectiveFromUtc = request.EffectiveFromUtc ?? DateTimeOffset.UtcNow,
            EffectiveToUtc = request.EffectiveToUtc,
            IsActive = true,
            Notes = request.Notes,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Rates.Add(rate);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(Rate),
            EntityId = rate.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.RateTypeId,
                request.EmployeeId,
                request.Amount,
                request.Location,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Notes
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(rate).Reference(r => r.RateType).LoadAsync(cancellationToken);
        await _db.Entry(rate).Reference(r => r.Employee).LoadAsync(cancellationToken);
        return rate;
    }

    public async Task<Rate> UpdateRateAsync(Guid tenantId, Guid rateId, UpdateRateRequest request, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var rate = await _db.Rates
            .FirstOrDefaultAsync(r => r.Id == rateId && r.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Rate not found.");

        var changes = new Dictionary<string, object>();

        if (rate.Amount != request.Amount)
        {
            changes["Amount"] = new { Old = rate.Amount, New = request.Amount };
            rate.Amount = request.Amount;
        }

        if (rate.Location != request.Location)
        {
            changes["Location"] = new { Old = rate.Location, New = request.Location };
            rate.Location = request.Location;
        }

        if (rate.EffectiveFromUtc != request.EffectiveFromUtc)
        {
            changes["EffectiveFromUtc"] = new { Old = rate.EffectiveFromUtc, New = request.EffectiveFromUtc };
            rate.EffectiveFromUtc = request.EffectiveFromUtc;
        }

        if (rate.EffectiveToUtc != request.EffectiveToUtc)
        {
            changes["EffectiveToUtc"] = new { Old = rate.EffectiveToUtc, New = request.EffectiveToUtc };
            rate.EffectiveToUtc = request.EffectiveToUtc;
        }

        if (rate.IsActive != request.IsActive)
        {
            changes["IsActive"] = new { Old = rate.IsActive, New = request.IsActive };
            rate.IsActive = request.IsActive;
        }

        if (rate.Notes != request.Notes)
        {
            changes["Notes"] = new { Old = rate.Notes, New = request.Notes };
            rate.Notes = request.Notes;
        }

        rate.ModifiedAtUtc = DateTimeOffset.UtcNow;

        if (changes.Any())
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityType = nameof(Rate),
                EntityId = rate.Id,
                Action = "Update",
                UserId = updatedByUserId,
                ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(rate).Reference(r => r.RateType).LoadAsync(cancellationToken);
        await _db.Entry(rate).Reference(r => r.Employee).LoadAsync(cancellationToken);
        return rate;
    }

    public async Task<decimal> GetApplicableRateAsync(Guid tenantId, Guid rateTypeId, Guid employeeId, string location, DateTimeOffset date, CancellationToken cancellationToken = default)
    {
        // First, try to find a specific rate for this employee and location
        var specificRate = await _db.Rates
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId &&
                       r.RateTypeId == rateTypeId &&
                       r.EmployeeId == employeeId &&
                       r.Location == location &&
                       r.IsActive &&
                       r.EffectiveFromUtc <= date &&
                       (!r.EffectiveToUtc.HasValue || r.EffectiveToUtc >= date))
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (specificRate != null)
            return specificRate.Amount;

        // If no specific rate, try to find a rate for this employee (any location)
        var employeeRate = await _db.Rates
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId &&
                       r.RateTypeId == rateTypeId &&
                       r.EmployeeId == employeeId &&
                       r.IsActive &&
                       r.EffectiveFromUtc <= date &&
                       (!r.EffectiveToUtc.HasValue || r.EffectiveToUtc >= date))
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (employeeRate != null)
            return employeeRate.Amount;

        // If no employee rate, use the default rate from the rate type
        var rateType = await _db.RateTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Id == rateTypeId && rt.TenantId == tenantId && rt.IsActive, cancellationToken);

        return rateType?.DefaultAmount ?? 0;
    }
}
