using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth;

/// <summary>Checks credentials without revealing whether an email exists or an agent is active.</summary>
public sealed class LoginCommandHandler(IAgentRepository agents, IAgentPasswordHasher passwordHasher)
{
    public async Task<LoginResult?> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (email.Length == 0 || password.Length == 0)
        {
            return null;
        }

        var agent = await agents.GetByEmailAsync(email, ct);
        if (agent is null || !agent.IsActive ||
            !passwordHasher.Verify(agent, password, agent.PasswordHash))
        {
            return null;
        }

        return new LoginResult(new AgentIdentityDto(agent.Id, agent.FullName, agent.Email));
    }
}
