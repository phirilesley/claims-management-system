using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ClaimsManagement.Api.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
}
