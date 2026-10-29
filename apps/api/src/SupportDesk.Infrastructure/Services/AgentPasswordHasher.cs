using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

/// <summary>Uses ASP.NET Core Identity's versioned PBKDF2 password hasher.</summary>
public sealed class AgentPasswordHasher : IAgentPasswordHasher
{
    private readonly PasswordHasher<Agent> _hasher = new();

    public string Hash(Agent agent, string password) => _hasher.HashPassword(agent, password);

    public bool Verify(Agent agent, string password, string? passwordHash) =>
        !string.IsNullOrWhiteSpace(passwordHash) &&
        _hasher.VerifyHashedPassword(agent, passwordHash, password) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
