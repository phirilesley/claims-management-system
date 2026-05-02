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
[Route("api/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IClaimWorkflowService _workflow;
    private readonly UserManager<ApplicationUser> _users;

    public ApprovalsController(IClaimWorkflowService workflow, UserManager<ApplicationUser> users)
    {
        _workflow = workflow;
        _users = users;
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<ApprovalQueueItemResponse>>> Queue(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user == null)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);
        var items = await _workflow.GetApprovalQueueAsync(userId.Value, roles, cancellationToken);

        var response = items.Select(i => new ApprovalQueueItemResponse(
            i.ClaimId,
            i.ReferenceNumber,
            i.Title,
            i.TotalAmount,
            i.CurrencyCode,
            i.SubmitterName,
            i.PendingStepName,
            i.StepOrder,
            i.SubmittedAtUtc)).ToList();

        return Ok(response);
    }

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
