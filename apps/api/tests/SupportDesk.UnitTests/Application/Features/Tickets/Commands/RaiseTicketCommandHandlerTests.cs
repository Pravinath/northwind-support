using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 72d)]
    [InlineData(TicketPriority.Medium, 24d)]
    [InlineData(TicketPriority.High, 8d)]
    [InlineData(TicketPriority.Critical, 4d)]
    public async Task HandleAsync_UsesConfiguredBaseWindowForStandardCustomer(TicketPriority priority, double expectedHours)
    {
        var context = new TicketCommandTestContext();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, false, false));

        await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(expectedHours), context.AddedTicket!.DueAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 36d)]
    [InlineData(TicketPriority.Medium, 12d)]
    [InlineData(TicketPriority.High, 4d)]
    [InlineData(TicketPriority.Critical, 2d)]
    public async Task HandleAsync_HalvesConfiguredWindowForPremiumCustomer(TicketPriority priority, double expectedHours)
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(expectedHours), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_PremiumWindowNeverFallsBelowConfiguredFloor()
    {
        var context = new TicketCommandTestContext();
        context.TriageOptions.BaseWindowHours["Low"] = 1.5;
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(true, false, false));

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(1), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_SelectsLeastLoadedEligibleSpecialistWithDeterministicTieBreak()
    {
        var context = new TicketCommandTestContext();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, true, false));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TriageAgent(1, "Inactive specialist", false, 10, 0, [3]),
                new TriageAgent(2, "Full specialist", true, 2, 2, [3]),
                new TriageAgent(3, "Wrong specialty", true, 10, 0, [9]),
                new TriageAgent(8, "Tie winner", true, 10, 2, [3]),
                new TriageAgent(7, "Tie loser", true, 10, 2, [3]),
                new TriageAgent(6, "More tickets", true, 10, 3, [3])
            ]);

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(7, context.AddedTicket!.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_CreatesUnassignedWithReason()
    {
        var context = new TicketCommandTestContext();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, true, false));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TriageAgent(3, "General agent", true, 10, 0, [9])]);

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Contains("No active agent", result.Triage!.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_CategoryRuleForcesCriticalAndReturnsTriageDecision()
    {
        var context = new TicketCommandTestContext();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(true, true, true));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TriageAgent(7, "Alex Turner", true, 5, 1, [3])]);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal(7, context.AddedTicket.AssignedAgentId);
        Assert.Equal(FixedClock.DefaultNow.AddHours(2), context.AddedTicket.DueAtUtc);
        Assert.Equal(TicketPriority.Critical, result.Triage!.Priority);
        Assert.Contains("Alex Turner", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
