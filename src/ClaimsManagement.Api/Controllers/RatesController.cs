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
public sealed class RatesController : ControllerBase
{
    private readonly IRateManagementService _rateManagement;
    private readonly UserManager<ApplicationUser> _users;

    public RatesController(IRateManagementService rateManagement, UserManager<ApplicationUser> users)
    {
        _rateManagement = rateManagement;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RateResponse>>> GetRates(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? rateTypeId,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var rates = await _rateManagement.GetRatesAsync(user.TenantId.Value, employeeId, rateTypeId, cancellationToken);
        var response = rates.Select(r => new RateResponse(
            r.Id,
            r.RateType.Id,
            r.RateType.Name,
            r.RateType.Code,
            r.Employee.Id,
            r.Employee.FullName,
            r.Amount,
            r.Location,
            r.EffectiveFromUtc,
            r.EffectiveToUtc,
            r.IsActive,
            r.Notes)).ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<RateResponse>> CreateRate([FromBody] CreateRateRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rate = await _rateManagement.CreateRateAsync(
                user.TenantId.Value,
                new CreateRateRequest(
                    request.RateTypeId,
                    request.EmployeeId,
                    request.Amount,
                    request.Location,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.Notes),
                userId.Value,
                cancellationToken);

            return Created($"/api/rates/{rate.Id}", new RateResponse(
                rate.Id,
                rate.RateType.Id,
                rate.RateType.Name,
                rate.RateType.Code,
                rate.Employee.Id,
                rate.Employee.FullName,
                rate.Amount,
                rate.Location,
                rate.EffectiveFromUtc,
                rate.EffectiveToUtc,
                rate.IsActive,
                rate.Notes));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RateResponse>> UpdateRate(Guid id, [FromBody] UpdateRateRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rate = await _rateManagement.UpdateRateAsync(
                user.TenantId.Value,
                id,
                new UpdateRateRequest(
                    request.Amount,
                    request.Location,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.IsActive,
                    request.Notes),
                userId.Value,
                cancellationToken);

            return Ok(new RateResponse(
                rate.Id,
                rate.RateType.Id,
                rate.RateType.Name,
                rate.RateType.Code,
                rate.Employee.Id,
                rate.Employee.FullName,
                rate.Amount,
                rate.Location,
                rate.EffectiveFromUtc,
                rate.EffectiveToUtc,
                rate.IsActive,
                rate.Notes));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("applicable")]
    public async Task<ActionResult<decimal>> GetApplicableRate(
        [FromQuery] Guid rateTypeId,
        [FromQuery] Guid employeeId,
        [FromQuery] string location,
        [FromQuery] DateTimeOffset date,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rate = await _rateManagement.GetApplicableRateAsync(user.TenantId.Value, rateTypeId, employeeId, location, date, cancellationToken);
            return Ok(rate);
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
