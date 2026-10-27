using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TriageDecision(TicketPriority Priority, DateTime DueAtUtc, SlaStatus SlaStatus,
    string PriorityReason, string SlaWindowReason, string AssignmentReason);
