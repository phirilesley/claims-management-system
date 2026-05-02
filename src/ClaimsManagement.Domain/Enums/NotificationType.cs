namespace ClaimsManagement.Domain.Enums;

public enum NotificationType
{
    ClaimSubmitted = 1,
    ClaimApproved = 2,
    ClaimRejected = 3,
    ClaimPaid = 4,
    PaymentBatchCreated = 5,
    PaymentBatchProcessed = 6,
    System = 7
}
