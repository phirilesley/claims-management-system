using System.Security.Claims;
using ClaimsManagement.Api.Contracts;
using DomainClaim = ClaimsManagement.Domain.Entities.Claim;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ClaimsController : ControllerBase
{
    private readonly IClaimsService _claims;
    private readonly IClaimWorkflowService _workflow;
    private readonly UserManager<ApplicationUser> _users;

    public ClaimsController(
        IClaimsService claims,
        IClaimWorkflowService workflow,
        UserManager<ApplicationUser> users)
    {
        _claims = claims;
        _workflow = workflow;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClaimSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var items = await _claims.GetClaimsForUserAsync(userId.Value, cancellationToken);
        var response = items.Select(MapSummary).ToList();
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ClaimSummaryResponse>> Create([FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var lines = request.Lines.Select(l => new CreateClaimLineModel(
            l.LineNumber,
            l.Description,
            l.Quantity,
            l.UnitAmount,
            l.Category,
            l.MileageKm,
            l.PerDiemDays,
            l.MetadataJson)).ToList();

        try
        {
            var claim = await _claims.CreateClaimAsync(
                userId.Value,
                request.ClaimTypeId,
                request.Title,
                request.CurrencyId,
                request.Submit,
                request.DynamicDataJson,
                request.BankDetailsJson,
                lines,
                cancellationToken);

            return Created($"/api/claims/{claim.Id}", MapSummary(claim));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ClaimSummaryResponse>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user == null)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);

        try
        {
            var claim = await _workflow.ApproveAsync(userId.Value, roles, id, cancellationToken);
            return Ok(MapSummary(claim));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ClaimSummaryResponse>> Reject(
        Guid id,
        [FromBody] RejectClaimRequest? body,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user == null)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);

        try
        {
            var claim = await _workflow.RejectAsync(userId.Value, roles, id, body?.Comment, cancellationToken);
            return Ok(MapSummary(claim));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static ClaimSummaryResponse MapSummary(DomainClaim c)
        => new(
            c.Id,
            c.ReferenceNumber,
            c.Title,
            c.Status.ToString(),
            c.Currency?.Code ?? string.Empty,
            c.TotalAmount,
            c.CreatedAtUtc,
            c.SubmittedAtUtc);
}
