namespace ClaimsManagement.Api.Contracts;

public sealed record ApprovalQueueItemResponse(
    Guid ClaimId,
    string ReferenceNumber,
    string Title,
    decimal TotalAmount,
    string CurrencyCode,
    string SubmitterName,
    string PendingStepName,
    int StepOrder,
    DateTimeOffset? SubmittedAtUtc);

public sealed record RejectClaimRequest(string? Comment);
