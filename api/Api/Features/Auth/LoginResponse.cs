namespace Api.Features.Auth;

// Deliberately no RefreshToken field - it only ever travels as an httpOnly cookie. See ADR 0008.
public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
