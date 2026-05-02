using ClaimsManagement.Api.Contracts;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public CatalogController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet("claim-types")]
    public async Task<ActionResult<IReadOnlyList<ClaimTypeOptionResponse>>> ListClaimTypes(CancellationToken cancellationToken)
    {
        var tenantId = await GetTenantIdAsync(cancellationToken);
        if (tenantId == null)
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Tenant context is required.");

        var items = await _db.ClaimTypes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new ClaimTypeOptionResponse(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyOptionResponse>>> Currencies(CancellationToken cancellationToken)
    {
        var items = await _db.Currencies
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new CurrencyOptionResponse(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    private async Task<Guid?> GetTenantIdAsync(CancellationToken cancellationToken)
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return null;

        var user = await _users.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user?.TenantId;
    }
}
