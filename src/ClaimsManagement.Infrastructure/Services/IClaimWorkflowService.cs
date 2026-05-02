using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Infrastructure.Services;

public interface IClaimWorkflowService
{
    Task<IReadOnlyList<ApprovalQueueItem>> GetApprovalQueueAsync(
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    Task<Claim> ApproveAsync(
        Guid userId,
        IEnumerable<string> roles,
        Guid claimId,
        CancellationToken cancellationToken = default);

    Task<Claim> RejectAsync(
        Guid userId,
        IEnumerable<string> roles,
        Guid claimId,
        string? comment,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalQueueItem(
    Guid ClaimId,
    string ReferenceNumber,
    string Title,
    decimal TotalAmount,
    string CurrencyCode,
    string SubmitterName,
    string PendingStepName,
    int StepOrder,
    DateTimeOffset? SubmittedAtUtc);
