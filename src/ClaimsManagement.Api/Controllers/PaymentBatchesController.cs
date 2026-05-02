using System.Security.Claims;
using ClaimsManagement.Api.Contracts;
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
public sealed class PaymentBatchesController : ControllerBase
{
    private readonly IPaymentBatchService _paymentBatches;
    private readonly UserManager<ApplicationUser> _users;

    public PaymentBatchesController(IPaymentBatchService paymentBatches, UserManager<ApplicationUser> users)
    {
        _paymentBatches = paymentBatches;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentBatchSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        var batches = await _paymentBatches.GetPaymentBatchesAsync(user.TenantId.Value, cancellationToken);
        var response = batches.Select(b => new PaymentBatchSummaryResponse(
            b.Id,
            b.Reference,
            b.Status,
            b.TotalAmount,
            b.CurrencyCode,
            b.PaymentCount,
            b.CreatedAtUtc,
            b.ProcessedAtUtc)).ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentBatchSummaryResponse>> Create([FromBody] CreatePaymentBatchRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        try
        {
            var batch = await _paymentBatches.CreatePaymentBatchAsync(
                user.TenantId.Value,
                request.ClaimIds,
                request.CurrencyId,
                request.Description,
                userId.Value,
                cancellationToken);

            return Created($"/api/paymentbatches/{batch.Id}", new PaymentBatchSummaryResponse(
                batch.Id,
                batch.Reference,
                batch.Status.ToString(),
                batch.TotalAmount,
                batch.Currency.Code,
                batch.PaymentCount,
                batch.CreatedAtUtc,
                batch.ProcessedAtUtc));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<PaymentBatchSummaryResponse>> Process(Guid id, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        try
        {
            var batch = await _paymentBatches.ProcessPaymentBatchAsync(
                user.TenantId.Value,
                id,
                userId.Value,
                cancellationToken);

            return Ok(new PaymentBatchSummaryResponse(
                batch.Id,
                batch.Reference,
                batch.Status.ToString(),
                batch.TotalAmount,
                batch.Currency.Code,
                batch.PaymentCount,
                batch.CreatedAtUtc,
                batch.ProcessedAtUtc));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        try
        {
            var exportBytes = await _paymentBatches.ExportPaymentBatchAsync(
                user.TenantId.Value,
                id,
                format.ToLower(),
                cancellationToken);

            var contentType = format.ToLower() switch
            {
                "csv" => "text/csv",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "text/csv"
            };

            var fileName = $"payment-batch-{id:N}.{format.ToLower()}";
            return File(exportBytes, contentType, fileName);
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
