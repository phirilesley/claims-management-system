namespace ClaimsManagement.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresAtUnixSeconds);

public sealed record RefreshRequest(string RefreshToken);
