using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using SecurityClaim = System.Security.Claims.Claim;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsManagement.Api.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public JwtTokenService(IOptions<JwtOptions> options, ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _options = options.Value;
        _db = db;
        _users = users;
    }

    public async Task<(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresUtc)> CreateTokensAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<SecurityClaim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"))
        };

        if (user.TenantId is { } tid)
            claims.Add(new SecurityClaim("tenant_id", tid.ToString()));

        claims.AddRange(roles.Select(r => new SecurityClaim(System.Security.Claims.ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(jwt);

        var refreshPlain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var refreshHash = Sha256Hex(refreshPlain);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return (accessToken, refreshPlain, expires);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresUtc)?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var hash = Sha256Hex(refreshToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (existing == null || existing.ExpiresAtUtc < DateTimeOffset.UtcNow)
            return null;

        var user = await _users.FindByIdAsync(existing.UserId.ToString());
        if (user == null)
            return null;

        _db.RefreshTokens.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);

        var roles = await _users.GetRolesAsync(user);
        return await CreateTokensAsync(user, roles, cancellationToken);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
