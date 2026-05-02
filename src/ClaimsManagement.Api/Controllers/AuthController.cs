using ClaimsManagement.Api.Contracts;
using ClaimsManagement.Api.Security;
using ClaimsManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenService _jwt;

    public AuthController(UserManager<ApplicationUser> users, IJwtTokenService jwt)
    {
        _users = users;
        _jwt = jwt;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized();

        if (!await _users.CheckPasswordAsync(user, request.Password))
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);
        var (access, refresh, expires) = await _jwt.CreateTokensAsync(user, roles, cancellationToken);

        return Ok(new TokenResponse(access, refresh, expires.ToUnixTimeSeconds()));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _jwt.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result == null)
            return Unauthorized();

        var (access, refresh, expires) = result.Value;
        return Ok(new TokenResponse(access, refresh, expires.ToUnixTimeSeconds()));
    }
}
