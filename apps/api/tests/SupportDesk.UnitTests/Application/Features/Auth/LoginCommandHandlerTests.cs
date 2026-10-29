using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithCorrectCredentials_ReturnsAgentIdentity()
    {
        var agent = NewAgent("alex.turner@northwind-support.example");
        agent.SetPasswordHash("stored-hash");
        var handler = CreateHandler(agent, passwordMatches: true);

        var result = await handler.HandleAsync(
            new LoginRequest(agent.Email, "Northwind-Local-Only-2026"), CancellationToken.None);

        Assert.Equal(agent.Email, result!.Agent.Email);
        Assert.Equal(agent.FullName, result.Agent.FullName);
    }

    [Fact]
    public async Task HandleAsync_WrongEmailAndWrongPasswordBothReturnNoIdentity()
    {
        var agent = NewAgent("alex.turner@northwind-support.example");
        agent.SetPasswordHash("stored-hash");
        var handler = CreateHandler(agent, passwordMatches: false);

        var wrongPassword = await handler.HandleAsync(
            new LoginRequest(agent.Email, "wrong-password"), CancellationToken.None);
        var wrongEmail = await handler.HandleAsync(
            new LoginRequest("unknown@northwind-support.example", "wrong-password"), CancellationToken.None);

        Assert.Null(wrongPassword);
        Assert.Null(wrongEmail);
    }

    [Fact]
    public async Task HandleAsync_InactiveAgentCannotAuthenticate()
    {
        var agent = NewAgent("yuki.tanaka@northwind-support.example");
        agent.SetPasswordHash("stored-hash");
        agent.Deactivate();
        var hasher = new Mock<IAgentPasswordHasher>(MockBehavior.Strict);
        var agents = new Mock<IAgentRepository>();
        agents.Setup(x => x.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var handler = new LoginCommandHandler(agents.Object, hasher.Object);

        var result = await handler.HandleAsync(new LoginRequest(agent.Email, "any-password"), CancellationToken.None);

        Assert.Null(result);
        hasher.VerifyNoOtherCalls();
    }

    private static Agent NewAgent(string email) =>
        new("Alex Turner", email, 10, DateTime.UtcNow);

    private static LoginCommandHandler CreateHandler(Agent agent, bool passwordMatches)
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(x => x.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var hasher = new Mock<IAgentPasswordHasher>();
        hasher.Setup(x => x.Verify(agent, It.IsAny<string>(), agent.PasswordHash)).Returns(passwordMatches);
        return new LoginCommandHandler(agents.Object, hasher.Object);
    }
}
