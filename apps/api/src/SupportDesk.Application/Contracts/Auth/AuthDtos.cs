namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AgentIdentityDto(int Id, string FullName, string Email);

public sealed record LoginResult(AgentIdentityDto Agent);

public sealed record CurrentAgentDto(int Id, string FullName, string Email);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentAgentDto Agent);
