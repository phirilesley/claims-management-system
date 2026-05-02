using System.Security.Claims;
using ClosedXML.Excel;
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
[EnableRateLimiting("Reporting")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportsService _reports;
    private readonly UserManager<ApplicationUser> _users;

    public ReportsController(IReportsService reports, UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _users = users;
    }

    [HttpGet("claims/excel")]
    public async Task<IActionResult> ClaimsExcel(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        var excelBytes = await _reports.GenerateClaimsReportAsync(
            user.TenantId.Value,
            from,
            to,
            departmentId,
            status,
            cancellationToken);

        return File(excelBytes, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"claims-report-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    [HttpGet("claims/pdf")]
    public async Task<IActionResult> ClaimsPdf(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        var pdfBytes = await _reports.GenerateClaimsPdfReportAsync(
            user.TenantId.Value,
            from,
            to,
            departmentId,
            status,
            cancellationToken);

        return File(pdfBytes, 
            "application/pdf",
            $"claims-report-{DateTime.UtcNow:yyyy-MM-dd}.pdf");
    }

    [HttpGet("payments/excel")]
    public async Task<IActionResult> PaymentsExcel(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? batchId,
        CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        var excelBytes = await _reports.GeneratePaymentsReportAsync(
            user.TenantId.Value,
            from,
            to,
            batchId,
            cancellationToken);

        return File(excelBytes, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"payments-report-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
