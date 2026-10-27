using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketEscalationRepository(SupportDbContext db) : ITicketEscalationRepository
{
    public async Task AddAsync(TicketEscalation escalation, CancellationToken ct) => await db.Set<TicketEscalation>().AddAsync(escalation, ct);
    public async Task<IReadOnlyList<TicketEscalation>> GetForTicketAsync(int ticketId, CancellationToken ct) =>
        await db.Set<TicketEscalation>().AsNoTracking().Where(x => x.TicketId == ticketId)
            .OrderByDescending(x => x.EscalatedAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
}
