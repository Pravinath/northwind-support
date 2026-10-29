using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class AgentPasswordHasherTests
{
    [Fact]
    public void Hash_UsesNonPlaintextSaltedValueThatVerifiesOnlyThePassword()
    {
        var agent = new Agent("Alex Turner", "alex@example.test", 10, DateTime.UtcNow);
        var hasher = new AgentPasswordHasher();

        var hash = hasher.Hash(agent, "Northwind-Local-Only-2026");

        Assert.NotEqual("Northwind-Local-Only-2026", hash);
        Assert.True(hasher.Verify(agent, "Northwind-Local-Only-2026", hash));
        Assert.False(hasher.Verify(agent, "incorrect-password", hash));
        Assert.False(hasher.Verify(agent, "incorrect-password", null));
    }
}
