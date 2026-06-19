namespace EProcurement.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUserDto(
    Guid Id,
    Guid? TenantId,
    string Email,
    string FullName,
    string Role);

public sealed record LoginResponse(string AccessToken, AuthenticatedUserDto User);
