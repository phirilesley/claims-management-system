using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public class ClaimsService : IClaimsService
{
    private static readonly (string Name, int Order)[] DefaultWorkflow =
    [
        ("Supervisor", 1),
        ("Finance", 2),
        ("Accounts", 3),
        ("Final", 4)
    ];

    private readonly ApplicationDbContext _db;

    public ClaimsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Claim> CreateClaimAsync(
        Guid userId,
        Guid claimTypeId,
        string title,
        Guid currencyId,
        bool submit,
        string? dynamicDataJson,
        string? bankDetailsJson,
        IReadOnlyList<CreateClaimLineModel> lines,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("No employee profile is linked to this user.");

        var claimType = await _db.ClaimTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == claimTypeId && c.TenantId == employee.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("Claim type was not found for this tenant.");

        var currency = await _db.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == currencyId && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Currency is not available.");

        if (lines.Count == 0)
            throw new InvalidOperationException("At least one claim line is required.");

        var lineEntities = new List<ClaimLine>();
        decimal total = 0;
        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            var lineTotal = line.Quantity * line.UnitAmount;
            total += lineTotal;
            lineEntities.Add(new ClaimLine
            {
                Id = Guid.NewGuid(),
                TenantId = employee.TenantId,
                LineNumber = line.LineNumber,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitAmount = line.UnitAmount,
                LineTotal = lineTotal,
                Category = line.Category,
                MileageKm = line.MileageKm,
                PerDiemDays = line.PerDiemDays,
                MetadataJson = line.MetadataJson,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var reference = await NextReferenceAsync(employee.TenantId, cancellationToken);

        var status = submit ? ClaimStatus.InReview : ClaimStatus.Draft;
        var now = DateTimeOffset.UtcNow;

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            TenantId = employee.TenantId,
            ClaimTypeId = claimTypeId,
            EmployeeId = employee.Id,
            Status = status,
            ReferenceNumber = reference,
            Title = title,
            CurrencyId = currencyId,
            TotalAmount = total,
            SubmittedAtUtc = submit ? now : null,
            DynamicDataJson = dynamicDataJson,
            BankDetailsJson = bankDetailsJson,
            CurrentWorkflowStep = submit ? 1 : 0,
            CreatedAtUtc = now,
            Lines = lineEntities
        };

        foreach (var l in claim.Lines)
            l.ClaimId = claim.Id;

        if (submit)
        {
            claim.StatusHistory.Add(new ClaimStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = claim.TenantId,
                ClaimId = claim.Id,
                FromStatus = ClaimStatus.Draft,
                ToStatus = ClaimStatus.InReview,
                ChangedByUserId = userId,
                ChangedAtUtc = now,
                Reason = "Submitted for approval",
                CreatedAtUtc = now
            });

            foreach (var (name, order) in DefaultWorkflow)
            {
                claim.Approvals.Add(new ClaimApproval
                {
                    Id = Guid.NewGuid(),
                    TenantId = claim.TenantId,
                    ClaimId = claim.Id,
                    StepOrder = order,
                    StepName = name,
                    Decision = ApprovalDecision.Pending,
                    CreatedAtUtc = now
                });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = claim.TenantId,
                EntityType = nameof(Claim),
                EntityId = claim.Id,
                Action = "Submit",
                UserId = userId,
                ChangesJson = null,
                CreatedAtUtc = now
            });
        }

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(claim).Reference(c => c.Currency).LoadAsync(cancellationToken);
        await _db.Entry(claim).Reference(c => c.ClaimType).LoadAsync(cancellationToken);
        return claim;
    }

    public async Task<IReadOnlyList<Claim>> GetClaimsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
        if (employee == null)
            return Array.Empty<Claim>();

        return await _db.Claims
            .AsNoTracking()
            .Include(c => c.Lines)
            .Include(c => c.Currency)
            .Include(c => c.ClaimType)
            .Where(c => c.EmployeeId == employee.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> NextReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"CM-{year}-";
        var count = await _db.Claims.CountAsync(
            c => c.TenantId == tenantId && c.ReferenceNumber.StartsWith(prefix),
            cancellationToken);
        return $"{prefix}{(count + 1):D5}";
    }
}
