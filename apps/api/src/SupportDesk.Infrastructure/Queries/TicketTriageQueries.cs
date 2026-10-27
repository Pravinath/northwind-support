using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TicketTriageQueries(SupportDbContext db) : ITicketTriageQueries
{
    public async Task<TicketTriageContext?> GetContextAsync(int customerId, int categoryId, CancellationToken ct)
    {
        var category = await db.Set<Category>().AsNoTracking().Where(x => x.Id == categoryId)
            .Select(x => new { x.RequiresSpecialist, x.ForcesCriticalPriority }).SingleOrDefaultAsync(ct);
        var tier = await db.Set<Customer>().AsNoTracking().Where(x => x.Id == customerId)
            .Select(x => x.Tier).SingleOrDefaultAsync(ct);
        if (category is null || !await db.Set<Customer>().AnyAsync(x => x.Id == customerId, ct)) return null;
        return new TicketTriageContext(tier == CustomerTier.Premium, category.RequiresSpecialist, category.ForcesCriticalPriority);
    }

    public async Task<IReadOnlyList<TriageAgent>> GetAgentsAsync(int categoryId, CancellationToken ct)
    {
        var agents = await db.Set<Agent>().AsNoTracking().Include(a => a.Specializations).ToListAsync(ct);
        var counts = await db.Set<Ticket>().AsNoTracking()
            .Where(t => t.AssignedAgentId != null && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .GroupBy(t => t.AssignedAgentId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return agents.Select(a => new TriageAgent(a.Id, a.FullName, a.IsActive, a.MaxOpenTickets,
            counts.GetValueOrDefault(a.Id), a.Specializations.Select(s => s.CategoryId).ToArray())).ToArray();
    }
}
