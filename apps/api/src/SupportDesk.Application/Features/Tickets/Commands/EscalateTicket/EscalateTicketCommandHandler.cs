using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(ITicketRepository tickets, ITicketEscalationRepository history,
    ITicketTriageQueries triage, IUnitOfWork unitOfWork, IClock clock, TicketTriageOptions options,
    GetTicketQueryHandler getTicket)
{
    public async Task<EscalationResult> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? "";
        var actor = request.EscalatedBy?.Trim() ?? "";
        if (reason.Length is < 5 or > 500) throw new ValidationException(new Dictionary<string, string[]> { ["Reason"] = ["Reason must contain between 5 and 500 characters."] });
        if (actor.Length is < 1 or > 100) throw new ValidationException(new Dictionary<string, string[]> { ["EscalatedBy"] = ["An actor is required (maximum 100 characters)."] });
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        if (ticket.Priority == TicketPriority.Critical) throw new BusinessRuleViolationException($"Ticket {ticket.Reference} is already Critical and cannot be escalated.");
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed) throw new BusinessRuleViolationException($"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        var context = await triage.GetContextAsync(ticket.CustomerId, ticket.CategoryId, ct)
            ?? throw new NotFoundException("Ticket triage data", id);
        var candidates = await triage.GetAgentsAsync(ticket.CategoryId, ct);
        bool Eligible(TriageAgent a) => a.IsActive && a.OpenTicketCount < a.MaxOpenTickets &&
            (!context.RequiresSpecialist || a.SpecializationCategoryIds.Contains(ticket.CategoryId));
        var assignedId = ticket.AssignedAgentId;
        var current = assignedId is null ? null : candidates.FirstOrDefault(a => a.Id == assignedId.Value);
        string assignmentReason;
        if (current is not null && Eligible(current)) assignmentReason = $"Kept current agent {current.FullName}; still eligible.";
        else
        {
            var next = candidates.Where(Eligible).OrderBy(a => a.OpenTicketCount).ThenBy(a => a.Id).FirstOrDefault();
            assignedId = next?.Id;
            assignmentReason = next is null ? "No eligible agent is available; ticket remains unassigned." : $"Assigned to {next.FullName}, the eligible agent with the fewest open tickets.";
        }
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var hours = options.WindowHours(nextPriority.ToString());
        if (context.IsPremium) hours = Math.Max(options.MinimumWindowHours, hours * options.PremiumCustomerMultiplier);
        var now = clock.UtcNow;
        var due = now.AddHours(hours);
        var audit = new TicketEscalation(ticket.Id, ticket.Priority, nextPriority, ticket.AssignedAgentId,
            assignedId, ticket.DueAtUtc, due, reason, actor, now);
        ticket.Escalate(nextPriority, assignedId, due, now);
        await history.AddAsync(audit, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var updated = await getTicket.HandleAsync(id, ct);
        updated.Triage = new TriageDecision(nextPriority, due,
            SlaEvaluator.Evaluate(due, null, now, options.AtRiskThresholdPercent, hours),
            $"Escalated from {audit.FromPriority} to {nextPriority}.", $"{hours:0.##} hour response window.", assignmentReason);
        return new EscalationResult(updated, new TicketEscalationDto(audit.Id, audit.FromPriority, audit.ToPriority,
            audit.FromAgentId, audit.ToAgentId, audit.FromDueAtUtc, audit.ToDueAtUtc, audit.Reason, audit.EscalatedBy, audit.EscalatedAtUtc));
    }
}
