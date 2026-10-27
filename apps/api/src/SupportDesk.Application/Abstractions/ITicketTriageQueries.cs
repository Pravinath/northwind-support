namespace SupportDesk.Application.Abstractions;

public interface ITicketTriageQueries
{
    Task<TicketTriageContext?> GetContextAsync(int customerId, int categoryId, CancellationToken ct);
    Task<IReadOnlyList<TriageAgent>> GetAgentsAsync(int categoryId, CancellationToken ct);
}

public sealed record TicketTriageContext(bool IsPremium, bool RequiresSpecialist, bool ForcesCriticalPriority);
public sealed record TriageAgent(int Id, string FullName, bool IsActive, int MaxOpenTickets,
    int OpenTicketCount, IReadOnlyCollection<int> SpecializationCategoryIds);
