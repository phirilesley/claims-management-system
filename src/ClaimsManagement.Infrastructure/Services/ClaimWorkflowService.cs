using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public class ClaimWorkflowService : IClaimWorkflowService
{
    private const int FinalStepOrder = 4;

    private readonly ApplicationDbContext _db;

    public ClaimWorkflowService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApprovalQueueItem>> GetApprovalQueueAsync(
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToList();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.TenantId == null)
            return Array.Empty<ApprovalQueueItem>();

        var tenantId = user.TenantId.Value;

        var claims = await _db.Claims
            .AsNoTracking()
            .Include(c => c.Employee)
            .Include(c => c.Currency)
            .Include(c => c.Approvals)
            .Where(c => c.TenantId == tenantId)
            .Where(c => c.Status == ClaimStatus.InReview || c.Status == ClaimStatus.Submitted)
            .ToListAsync(cancellationToken);

        var result = new List<ApprovalQueueItem>();
        foreach (var c in claims)
        {
            if (c.Employee.UserId == userId)
                continue;

            var pending = c.Approvals.FirstOrDefault(a =>
                a.StepOrder == c.CurrentWorkflowStep && a.Decision == ApprovalDecision.Pending);
            if (pending == null)
                continue;

            if (!CanApproveStep(roleList, c.CurrentWorkflowStep))
                continue;

            result.Add(new ApprovalQueueItem(
                c.Id,
                c.ReferenceNumber,
                c.Title,
                c.TotalAmount,
                c.Currency?.Code ?? "",
                c.Employee.FullName,
                pending.StepName,
                pending.StepOrder,
                c.SubmittedAtUtc));
        }

        return result.OrderByDescending(x => x.SubmittedAtUtc).ToList();
    }

    public async Task<Claim> ApproveAsync(
        Guid userId,
        IEnumerable<string> roles,
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToList();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User was not found.");

        if (user.TenantId == null)
            throw new InvalidOperationException("Tenant context is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var claim = await _db.Claims
            .Include(c => c.Employee)
            .Include(c => c.Approvals)
            .FirstOrDefaultAsync(
                c => c.Id == claimId && c.TenantId == user.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("Claim was not found.");

        if (claim.Employee.UserId == userId)
            throw new InvalidOperationException("You cannot approve your own claim.");

        if (claim.Status != ClaimStatus.InReview && claim.Status != ClaimStatus.Submitted)
            throw new InvalidOperationException("This claim is not awaiting approval.");

        var pending = claim.Approvals.FirstOrDefault(a =>
            a.StepOrder == claim.CurrentWorkflowStep && a.Decision == ApprovalDecision.Pending)
            ?? throw new InvalidOperationException("No pending approval step matches the workflow.");

        if (!CanApproveStep(roleList, claim.CurrentWorkflowStep))
            throw new InvalidOperationException("You are not allowed to approve at this workflow step.");

        var now = DateTimeOffset.UtcNow;
        pending.Decision = ApprovalDecision.Approved;
        pending.ApproverUserId = userId;
        pending.ActionAtUtc = now;
        pending.ModifiedAtUtc = now;

        if (claim.CurrentWorkflowStep < FinalStepOrder)
        {
            claim.CurrentWorkflowStep++;
            claim.ModifiedAtUtc = now;
            claim.Status = ClaimStatus.InReview;
        }
        else
        {
            var prev = claim.Status;
            claim.Status = ClaimStatus.Approved;
            claim.ModifiedAtUtc = now;
            claim.StatusHistory.Add(new ClaimStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = claim.TenantId,
                ClaimId = claim.Id,
                FromStatus = prev,
                ToStatus = ClaimStatus.Approved,
                ChangedByUserId = userId,
                ChangedAtUtc = now,
                Reason = "Final approval",
                CreatedAtUtc = now
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = claim.TenantId,
            EntityType = nameof(Claim),
            EntityId = claim.Id,
            Action = "Approve",
            UserId = userId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                step = pending.StepOrder,
                stepName = pending.StepName
            }),
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _db.Entry(claim).Reference(c => c.Currency).LoadAsync(cancellationToken);
        await _db.Entry(claim).Reference(c => c.ClaimType).LoadAsync(cancellationToken);
        return claim;
    }

    public async Task<Claim> RejectAsync(
        Guid userId,
        IEnumerable<string> roles,
        Guid claimId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToList();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User was not found.");

        if (user.TenantId == null)
            throw new InvalidOperationException("Tenant context is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var claim = await _db.Claims
            .Include(c => c.Employee)
            .Include(c => c.Approvals)
            .FirstOrDefaultAsync(
                c => c.Id == claimId && c.TenantId == user.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("Claim was not found.");

        if (claim.Employee.UserId == userId)
            throw new InvalidOperationException("You cannot reject your own claim.");

        if (claim.Status != ClaimStatus.InReview && claim.Status != ClaimStatus.Submitted)
            throw new InvalidOperationException("This claim is not awaiting approval.");

        var pending = claim.Approvals.FirstOrDefault(a =>
            a.StepOrder == claim.CurrentWorkflowStep && a.Decision == ApprovalDecision.Pending)
            ?? throw new InvalidOperationException("No pending approval step matches the workflow.");

        if (!CanApproveStep(roleList, claim.CurrentWorkflowStep))
            throw new InvalidOperationException("You are not allowed to act at this workflow step.");

        var now = DateTimeOffset.UtcNow;
        var prev = claim.Status;

        pending.Decision = ApprovalDecision.Rejected;
        pending.ApproverUserId = userId;
        pending.ActionAtUtc = now;
        pending.Comment = comment;
        pending.ModifiedAtUtc = now;

        claim.Status = ClaimStatus.Rejected;
        claim.ModifiedAtUtc = now;

        claim.StatusHistory.Add(new ClaimStatusHistory
        {
            Id = Guid.NewGuid(),
            TenantId = claim.TenantId,
            ClaimId = claim.Id,
            FromStatus = prev,
            ToStatus = ClaimStatus.Rejected,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            Reason = comment ?? "Rejected",
            CreatedAtUtc = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = claim.TenantId,
            EntityType = nameof(Claim),
            EntityId = claim.Id,
            Action = "Reject",
            UserId = userId,
            ChangesJson = null,
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _db.Entry(claim).Reference(c => c.Currency).LoadAsync(cancellationToken);
        await _db.Entry(claim).Reference(c => c.ClaimType).LoadAsync(cancellationToken);
        return claim;
    }

    private static bool CanApproveStep(IReadOnlyList<string> roles, int stepOrder)
    {
        if (roles.Contains("Admin"))
            return true;

        return stepOrder switch
        {
            1 => roles.Contains("Supervisor"),
            2 => roles.Contains("Finance"),
            3 => roles.Contains("Finance"),
            4 => roles.Contains("Finance") || roles.Contains("Supervisor"),
            _ => false
        };
    }
}
