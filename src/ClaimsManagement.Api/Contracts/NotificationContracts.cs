namespace ClaimsManagement.Api.Contracts;

public record NotificationResponse(
    Guid Id,
    string Title,
    string Body,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    string? EntityType,
    Guid? EntityId);

public record NotificationCountResponse(
    int UnreadCount);
