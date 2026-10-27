using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, TicketTriageOptions options) : ITicketQueries
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tickets = ApplyFilters(TicketsWithLabels(), query, now, options);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    options.AtRiskThresholdPercent,
                    x.Customer.Tier == CustomerTier.Premium
                        ? Math.Max(options.MinimumWindowHours, WindowHours(options, x.Ticket.Priority) * options.PremiumCustomerMultiplier)
                        : WindowHours(options, x.Ticket.Priority))
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    options.AtRiskThresholdPercent,
                    x.Customer.Tier == CustomerTier.Premium
                        ? Math.Max(options.MinimumWindowHours, WindowHours(options, x.Ticket.Priority) * options.PremiumCustomerMultiplier)
                        : WindowHours(options, x.Ticket.Priority))
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    options.AtRiskThresholdPercent,
                    x.Customer.Tier == CustomerTier.Premium
                        ? Math.Max(options.MinimumWindowHours, WindowHours(options, x.Ticket.Priority) * options.PremiumCustomerMultiplier)
                        : WindowHours(options, x.Ticket.Priority))
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private static IQueryable<TicketWithLabels> ApplyFilters(IQueryable<TicketWithLabels> source, TicketQuery query, DateTime now, TicketTriageOptions options)
    {
        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } assignedAgentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == assignedAgentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (query.Search is { Length: > 0 } search)
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.SlaStatus is { } sla)
        {
            var riskLow = options.WindowHours("Low") * options.AtRiskThresholdPercent / 100;
            var riskMedium = options.WindowHours("Medium") * options.AtRiskThresholdPercent / 100;
            var riskHigh = options.WindowHours("High") * options.AtRiskThresholdPercent / 100;
            var riskCritical = options.WindowHours("Critical") * options.AtRiskThresholdPercent / 100;
            var premiumRiskLow = Math.Max(options.MinimumWindowHours, options.WindowHours("Low") * options.PremiumCustomerMultiplier) * options.AtRiskThresholdPercent / 100;
            var premiumRiskMedium = Math.Max(options.MinimumWindowHours, options.WindowHours("Medium") * options.PremiumCustomerMultiplier) * options.AtRiskThresholdPercent / 100;
            var premiumRiskHigh = Math.Max(options.MinimumWindowHours, options.WindowHours("High") * options.PremiumCustomerMultiplier) * options.AtRiskThresholdPercent / 100;
            var premiumRiskCritical = Math.Max(options.MinimumWindowHours, options.WindowHours("Critical") * options.PremiumCustomerMultiplier) * options.AtRiskThresholdPercent / 100;
            source = sla switch
            {
                SlaStatus.NotApplicable => source.Where(x => x.Ticket.DueAtUtc == null),
                SlaStatus.Met => source.Where(x => x.Ticket.ResolvedAtUtc != null && x.Ticket.DueAtUtc != null && x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),
                SlaStatus.Breached => source.Where(x => x.Ticket.DueAtUtc != null &&
                    ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) || (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < now))),
                SlaStatus.AtRisk => source.Where(x => x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc != null && x.Ticket.DueAtUtc >= now &&
                    x.Ticket.DueAtUtc <= now.AddHours(x.Customer.Tier == CustomerTier.Premium
                        ? (x.Ticket.Priority == TicketPriority.Critical ? premiumRiskCritical : x.Ticket.Priority == TicketPriority.High ? premiumRiskHigh : x.Ticket.Priority == TicketPriority.Low ? premiumRiskLow : premiumRiskMedium)
                        : (x.Ticket.Priority == TicketPriority.Critical ? riskCritical : x.Ticket.Priority == TicketPriority.High ? riskHigh : x.Ticket.Priority == TicketPriority.Low ? riskLow : riskMedium))),
                SlaStatus.WithinSla => source.Where(x => x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc != null &&
                    x.Ticket.DueAtUtc > now.AddHours(x.Customer.Tier == CustomerTier.Premium
                        ? (x.Ticket.Priority == TicketPriority.Critical ? premiumRiskCritical : x.Ticket.Priority == TicketPriority.High ? premiumRiskHigh : x.Ticket.Priority == TicketPriority.Low ? premiumRiskLow : premiumRiskMedium)
                        : (x.Ticket.Priority == TicketPriority.Critical ? riskCritical : x.Ticket.Priority == TicketPriority.High ? riskHigh : x.Ticket.Priority == TicketPriority.Low ? riskLow : riskMedium))),
                _ => source
            };
        }

        return source;
    }

    private static double WindowHours(TicketTriageOptions options, TicketPriority priority) => options.WindowHours(priority.ToString());


    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
