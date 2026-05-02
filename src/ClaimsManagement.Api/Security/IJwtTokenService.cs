using ClaimsManagement.Infrastructure.Identity;

namespace ClaimsManagement.Api.Security;

public interface IJwtTokenService
{
    Task<(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresUtc)> CreateTokensAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    Task<(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresUtc)?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
