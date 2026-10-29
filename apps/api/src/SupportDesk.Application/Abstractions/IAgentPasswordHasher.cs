using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>Hashes and verifies passwords using an established password-hashing implementation.</summary>
public interface IAgentPasswordHasher
{
    string Hash(Agent agent, string password);

    bool Verify(Agent agent, string password, string? passwordHash);
}
