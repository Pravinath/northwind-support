using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_RaisesOneLevel_RecomputesDueAndRecordsHistoryWhileKeepingEligibleAgent()
    {
        var context = new TicketCommandTestContext();
        var oldDue = FixedClock.DefaultNow.AddHours(20);
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(4).Build();
        ticket.ApplyTriage(TicketPriority.Medium, 4, oldDue, FixedClock.DefaultNow);
        context.ExistingTicket(ticket);
        context.CaptureEscalation();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, false, false));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TriageAgent(4, "Sara Lindqvist", true, 5, 2, [])]);
        context.Clock.Advance(TimeSpan.FromMinutes(30));
        var now = context.Clock.UtcNow;

        var result = await context.EscalateTicket.HandleAsync(0,
            new EscalateTicketRequest("Customer impact has increased."), "Team lead", CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(now.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(TicketPriority.Medium, context.AddedEscalation.FromPriority);
        Assert.Equal(TicketPriority.High, context.AddedEscalation.ToPriority);
        Assert.Equal(4, context.AddedEscalation.FromAgentId);
        Assert.Equal(4, context.AddedEscalation.ToAgentId);
        Assert.Equal(oldDue, context.AddedEscalation.FromDueAtUtc);
        Assert.Equal(now.AddHours(8), context.AddedEscalation.ToDueAtUtc);
        Assert.Equal("Customer impact has increased.", context.AddedEscalation.Reason);
        Assert.Equal("Team lead", context.AddedEscalation.EscalatedBy);
        Assert.Equal(now, context.AddedEscalation.EscalatedAtUtc);
        Assert.Equal(context.AddedEscalation.Id, result.Escalation.Id);
        context.EscalationHistory.Verify(h => h.AddAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Once());
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenCurrentAgentIsNoLongerEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(4).Build();
        context.ExistingTicket(ticket);
        context.CaptureEscalation();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, true, false));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TriageAgent(4, "Sara", true, 2, 2, [1]),
                new TriageAgent(8, "General agent", true, 10, 0, []),
                new TriageAgent(7, "Specialist", true, 10, 3, [1]),
                new TriageAgent(6, "Least loaded specialist", true, 10, 1, [1])
            ]);

        await context.EscalateTicket.HandleAsync(0,
            new EscalateTicketRequest("Need specialist ownership."), "Team lead", CancellationToken.None);

        Assert.Equal(6, ticket.AssignedAgentId);
        Assert.Equal(4, context.AddedEscalation!.FromAgentId);
        Assert.Equal(6, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentQualifies_UnassignsAndRecordsNullDestination()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(4).Build();
        context.ExistingTicket(ticket);
        context.CaptureEscalation();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, true, false));
        context.TriageQueries.Setup(q => q.GetAgentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TriageAgent(4, "Sara", false, 5, 1, [1])]);

        var result = await context.EscalateTicket.HandleAsync(0,
            new EscalateTicketRequest("No agent can take this."), "Team lead", CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(context.AddedEscalation!.ToAgentId);
        Assert.Contains("No eligible agent", result.Ticket.Triage!.AssignmentReason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.InProgress)]
    [InlineData(TicketPriority.High, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketCannotBeEscalated_RejectsWithoutSaving(TicketPriority priority, TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).Build();
        context.ExistingTicket(ticket);
        context.CaptureEscalation();

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => context.EscalateTicket.HandleAsync(
            0, new EscalateTicketRequest("Escalation reason."), "Team lead", CancellationToken.None));

        Assert.Contains(priority == TicketPriority.Critical ? "Critical" : status.ToString(), exception.Message);
        context.EscalationHistory.Verify(h => h.AddAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Never());
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFound()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => context.EscalateTicket.HandleAsync(
            999, new EscalateTicketRequest("Escalation reason."), "Team lead", CancellationToken.None));
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("four", "Team lead")]
    [InlineData("Valid escalation reason.", "")]
    [InlineData("Valid escalation reason.", "This actor name is intentionally longer than one hundred characters to verify validation rejects an oversized actor value before any ticket lookup or database write is performed by this handler.")]
    public async Task HandleAsync_WhenReasonOrActorIsInvalid_RejectsBeforeLoadingTicket(string reason, string actor)
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.EscalateTicket.HandleAsync(
            1, new EscalateTicketRequest(reason), actor, CancellationToken.None));

        context.Tickets.Verify(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_AcceptsReasonAtMaximumLength()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();
        context.ExistingTicket(ticket);
        context.CaptureEscalation();
        context.TriageQueries.Setup(q => q.GetContextAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageContext(false, false, false));
        var reason = new string('R', 500);

        await context.EscalateTicket.HandleAsync(0,
            new EscalateTicketRequest(reason), "Lead", CancellationToken.None);

        Assert.Equal(500, context.AddedEscalation!.Reason.Length);
    }

    [Fact]
    public async Task HandleAsync_RejectsReasonOverMaximumLength()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.EscalateTicket.HandleAsync(
            1, new EscalateTicketRequest(new string('R', 501)), "Team lead", CancellationToken.None));

        context.Tickets.Verify(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        context.VerifySaved(Times.Never());
    }
}
