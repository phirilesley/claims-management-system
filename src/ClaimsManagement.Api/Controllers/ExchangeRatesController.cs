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
public sealed class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly UserManager<ApplicationUser> _users;

    public ExchangeRatesController(IExchangeRateService exchangeRateService, UserManager<ApplicationUser> users)
    {
        _exchangeRateService = exchangeRateService;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateResponse>>> GetExchangeRates(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var rates = await _exchangeRateService.GetExchangeRatesAsync(user.TenantId.Value, cancellationToken);
        var response = rates.Select(er => new ExchangeRateResponse(
            er.Id,
            er.FromCurrency.Id,
            er.FromCurrency.Code,
            er.FromCurrency.Name,
            er.ToCurrency.Id,
            er.ToCurrency.Code,
            er.ToCurrency.Name,
            er.Rate,
            er.EffectiveFromUtc,
            er.EffectiveToUtc,
            er.IsActive,
            er.Source)).ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ExchangeRateResponse>> CreateExchangeRate([FromBody] CreateExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rate = await _exchangeRateService.CreateExchangeRateAsync(
                user.TenantId.Value,
                new CreateExchangeRateRequest(
                    request.FromCurrencyId,
                    request.ToCurrencyId,
                    request.Rate,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.Source),
                userId.Value,
                cancellationToken);

            return Created($"/api/exchangerates/{rate.Id}", new ExchangeRateResponse(
                rate.Id,
                rate.FromCurrency.Id,
                rate.FromCurrency.Code,
                rate.FromCurrency.Name,
                rate.ToCurrency.Id,
                rate.ToCurrency.Code,
                rate.ToCurrency.Name,
                rate.Rate,
                rate.EffectiveFromUtc,
                rate.EffectiveToUtc,
                rate.IsActive,
                rate.Source));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExchangeRateResponse>> UpdateExchangeRate(Guid id, [FromBody] UpdateExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var rate = await _exchangeRateService.UpdateExchangeRateAsync(
                user.TenantId.Value,
                id,
                new UpdateExchangeRateRequest(
                    request.Rate,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.IsActive,
                    request.Source),
                userId.Value,
                cancellationToken);

            return Ok(new ExchangeRateResponse(
                rate.Id,
                rate.FromCurrency.Id,
                rate.FromCurrency.Code,
                rate.FromCurrency.Name,
                rate.ToCurrency.Id,
                rate.ToCurrency.Code,
                rate.ToCurrency.Name,
                rate.Rate,
                rate.EffectiveFromUtc,
                rate.EffectiveToUtc,
                rate.IsActive,
                rate.Source));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteExchangeRate(Guid id, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var deleted = await _exchangeRateService.DeleteExchangeRateAsync(user.TenantId.Value, id, userId.Value, cancellationToken);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyOptionResponse>>> GetCurrencies(CancellationToken cancellationToken)
    {
        var currencies = await _exchangeRateService.GetAvailableCurrenciesAsync(cancellationToken);
        var response = currencies.Select(c => new CurrencyOptionResponse(c.Id, c.Code, c.Name)).ToList();
        return Ok(response);
    }

    [HttpPost("convert")]
    public async Task<ActionResult<decimal>> ConvertCurrency([FromBody] ConvertCurrencyRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var convertedAmount = await _exchangeRateService.ConvertCurrencyAsync(
                user.TenantId.Value,
                request.Amount,
                request.FromCurrencyId,
                request.ToCurrencyId,
                request.Date,
                cancellationToken);

            return Ok(convertedAmount);
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
