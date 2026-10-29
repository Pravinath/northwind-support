using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record EscalateTicketRequest(string Reason);
public sealed record TicketEscalationDto(int Id, TicketPriority FromPriority, TicketPriority ToPriority,
    int? FromAgentId, int? ToAgentId, DateTime? FromDueAtUtc, DateTime? ToDueAtUtc,
    string Reason, string EscalatedBy, DateTime EscalatedAtUtc);
public sealed record EscalationResult(TicketDetailDto Ticket, TicketEscalationDto Escalation);
