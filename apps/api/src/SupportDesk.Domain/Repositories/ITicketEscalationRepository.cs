using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

public interface ITicketEscalationRepository
{
    Task AddAsync(TicketEscalation escalation, CancellationToken ct);
    Task<IReadOnlyList<TicketEscalation>> GetForTicketAsync(int ticketId, CancellationToken ct);
}
