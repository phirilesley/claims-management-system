using System.Security.Claims;
using ClaimsManagement.Contracts;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[EnableRateLimiting("Authenticated")]
public sealed class RateTypesController : ControllerBase
{
    private readonly IRateManagementService _rateManagement;
    private readonly UserManager<ApplicationUser> _users;

    public RateTypesController(IRateManagementService rateManagement, UserManager<ApplicationUser> users)
    {
        _rateManagement = rateManagement;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RateTypeResponse>>> GetRateTypes(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var rateTypes = await _rateManagement.GetRateTypesAsync(user.TenantId.Value, cancellationToken);
        var response = rateTypes.Select(rt => new RateTypeResponse(
            rt.Id,
            rt.Code,
            rt.Name,
            rt.Description,
            rt.Unit,
            rt.DefaultAmount,
            rt.Currency.Code,
            rt.RequiresReceipt,
            rt.MaxDailyAmount,
            rt.MaxOccurrencesPerDay,
            rt.Category)).ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<RateTypeResponse>> CreateRateType([FromBody] CreateRateTypeRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rateType = await _rateManagement.CreateRateTypeAsync(
                user.TenantId.Value,
                new CreateRateTypeRequest(
                    request.Name,
                    request.Code,
                    request.Description,
                    request.Unit,
                    request.DefaultAmount,
                    request.CurrencyId,
                    request.RequiresReceipt,
                    request.MaxDailyAmount,
                    request.MaxOccurrencesPerDay,
                    request.Category),
                userId.Value,
                cancellationToken);

            return Created($"/api/ratetypes/{rateType.Id}", new RateTypeResponse(
                rateType.Id,
                rateType.Code,
                rateType.Name,
                rateType.Description,
                rateType.Unit,
                rateType.DefaultAmount,
                rateType.Currency.Code,
                rateType.RequiresReceipt,
                rateType.MaxDailyAmount,
                rateType.MaxOccurrencesPerDay,
                rateType.Category));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RateTypeResponse>> UpdateRateType(Guid id, [FromBody] UpdateRateTypeRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rateType = await _rateManagement.UpdateRateTypeAsync(
                user.TenantId.Value,
                id,
                new UpdateRateTypeRequest(
                    request.Name,
                    request.Description,
                    request.Unit,
                    request.DefaultAmount,
                    request.RequiresReceipt,
                    request.MaxDailyAmount,
                    request.MaxOccurrencesPerDay,
                    request.Category),
                userId.Value,
                cancellationToken);

            return Ok(new RateTypeResponse(
                rateType.Id,
                rateType.Code,
                rateType.Name,
                rateType.Description,
                rateType.Unit,
                rateType.DefaultAmount,
                rateType.Currency.Code,
                rateType.RequiresReceipt,
                rateType.MaxDailyAmount,
                rateType.MaxOccurrencesPerDay,
                rateType.Category));
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
}
