using System.Security.Claims;
using ClaimsManagement.Api.Contracts;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationsController(INotificationService notifications, UserManager<ApplicationUser> users)
    {
        _notifications = notifications;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var notifications = await _notifications.GetUserNotificationsAsync(
            user.TenantId.Value,
            userId.Value,
            unreadOnly,
            limit,
            cancellationToken);

        var response = notifications.Select(n => new NotificationResponse(
            n.Id,
            n.Title,
            n.Body,
            n.Type.ToString(),
            n.IsRead,
            n.CreatedAtUtc,
            n.EntityType,
            n.EntityId)).ToList();

        return Ok(response);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            await _notifications.MarkAsReadAsync(user.TenantId.Value, userId.Value, id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        await _notifications.MarkAllAsReadAsync(user.TenantId.Value, userId.Value, cancellationToken);
        return NoContent();
    }

    [HttpGet("count")]
    public async Task<ActionResult<NotificationCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var count = await _notifications.GetUnreadCountAsync(user.TenantId.Value, userId.Value, cancellationToken);
        return Ok(new NotificationCountResponse(count));
    }

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
