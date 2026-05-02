using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public interface INotificationService
{
    Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task CreateNotificationAsync(
        Guid tenantId,
        Guid userId,
        string title,
        string body,
        NotificationType type,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(
        Guid tenantId,
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId && n.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Notification not found.");

        notification.IsRead = true;
        notification.ReadAtUtc = DateTimeOffset.UtcNow;
        notification.ModifiedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .Where(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
            notification.ModifiedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task CreateNotificationAsync(
        Guid tenantId,
        Guid userId,
        string title,
        string body,
        NotificationType type,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            EntityType = entityType,
            EntityId = entityId,
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        // TODO: Send real-time notification via SignalR
        // TODO: Send email notification if configured
    }
}
