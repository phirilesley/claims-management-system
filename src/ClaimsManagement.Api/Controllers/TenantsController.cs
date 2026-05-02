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
public sealed class TenantsController : ControllerBase
{
    private readonly ITenantManagementService _tenantManagement;
    private readonly UserManager<ApplicationUser> _users;

    public TenantsController(ITenantManagementService tenantManagement, UserManager<ApplicationUser> users)
    {
        _tenantManagement = tenantManagement;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<TenantDetailsResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var tenant = await _tenantManagement.GetTenantAsync(user.TenantId.Value, cancellationToken);
        if (tenant == null)
            return NotFound("Tenant not found.");

        return Ok(new TenantDetailsResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.EmployeeCount,
            tenant.DepartmentCount,
            tenant.CreatedAtUtc));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<TenantUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var users = await _tenantManagement.GetTenantUsersAsync(user.TenantId.Value, cancellationToken);
        var response = users.Select(u => new TenantUserResponse(
            u.Id,
            u.Email,
            u.FullName,
            u.Roles.ToList().ToList(),
            u.IsActive,
            u.CreatedAtUtc)).ToList();

        return Ok(response);
    }

    [HttpPost("users")]
    public async Task<ActionResult<TenantUserResponse>> CreateUser([FromBody] CreateTenantUserRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var currentUser = await _users.FindByIdAsync(userId.Value.ToString());
        if (currentUser?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var newUser = await _tenantManagement.CreateTenantUserAsync(
                currentUser.TenantId.Value,
                request.Email,
                request.FullName,
                request.Roles,
                request.DepartmentId,
                userId.Value,
                cancellationToken);

            return Created($"/api/tenants/users/{newUser.Id}", new TenantUserResponse(
                newUser.Id,
                newUser.Email ?? "",
                newUser.FullName,
                new List<string>(),
                newUser.IsActive,
                newUser.CreatedAtUtc));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> GetDepartments(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var departments = await _tenantManagement.GetDepartmentsAsync(user.TenantId.Value, cancellationToken);
        var response = departments.Select(d => new DepartmentResponse(
            d.Id,
            d.Code,
            d.Name,
            0,
            d.IsActive)).ToList();

        return Ok(response);
    }

    [HttpPost("departments")]
    public async Task<ActionResult<DepartmentResponse>> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var department = await _tenantManagement.CreateDepartmentAsync(
                user.TenantId.Value,
                request.Code,
                request.Name,
                userId.Value,
                cancellationToken);

            return Created($"/api/tenants/departments/{department.Id}", new DepartmentResponse(
                department.Id,
                department.Code,
                department.Name,
                0,
                department.IsActive));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("claim-types")]
    public async Task<ActionResult<ClaimTypeResponse>> CreateClaimType([FromBody] CreateClaimTypeRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var claimType = await _tenantManagement.CreateClaimTypeAsync(
                user.TenantId.Value,
                request.Code,
                request.Name,
                request.Description,
                request.FormSchemaJson,
                request.WorkflowSteps,
                userId.Value,
                cancellationToken);

            return Created($"/api/tenants/claim-types/{claimType.Id}", new ClaimTypeResponse(
                claimType.Id,
                claimType.Code,
                claimType.Name,
                claimType.Description ?? "",
                claimType.FormSchemaJson,
                claimType.WorkflowSteps));
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
