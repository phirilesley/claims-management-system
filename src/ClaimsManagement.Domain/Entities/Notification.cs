using ClaimsManagement.Domain.Common;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
}
