using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Services;

public sealed class JwtTokenService(IConfiguration configuration, IClock clock)
{
    public LoginResponse Create(LoginResult login)
    {
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var signingKey = configuration["Jwt:SigningKey"]!;
        var lifetimeMinutes = int.Parse(configuration["Jwt:LifetimeMinutes"]!);
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(lifetimeMinutes);
        var claims = new[]
        {
            new Claim("agentId", login.Agent.Id.ToString()),
            new Claim(ClaimTypes.Name, login.Agent.FullName),
            new Claim(ClaimTypes.Email, login.Agent.Email)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, now, expiresAt, credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt,
            new CurrentAgentDto(login.Agent.Id, login.Agent.FullName, login.Agent.Email));
    }
}
