using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger,
    ITicketTriageQueries triageQueries,
    TicketTriageOptions triageOptions)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var context = await triageQueries.GetContextAsync(request.CustomerId, request.CategoryId, ct)
            ?? throw new NotFoundException("Customer or category", request.CustomerId);
        var now = clock.UtcNow;
        var priority = context.ForcesCriticalPriority ? TicketPriority.Critical : request.RequestedPriority ?? TicketPriority.Medium;
        var window = triageOptions.WindowHours(priority.ToString());
        if (context.IsPremium) window = Math.Max(triageOptions.MinimumWindowHours, window * triageOptions.PremiumCustomerMultiplier);
        var eligible = (await triageQueries.GetAgentsAsync(request.CategoryId, ct))
            .Where(a => a.IsActive && a.OpenTicketCount < a.MaxOpenTickets &&
                (!context.RequiresSpecialist || a.SpecializationCategoryIds.Contains(request.CategoryId)))
            .OrderBy(a => a.OpenTicketCount).ThenBy(a => a.Id).ToArray();
        var assigned = eligible.FirstOrDefault();
        var assignmentReason = assigned is null
            ? "No active agent with the required capacity and specialization is available; ticket remains unassigned."
            : $"Assigned to {assigned.FullName}, the eligible agent with the fewest open tickets (tie-break: lowest agent ID).";
        var dueAt = now.AddHours(window);
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now);
        ticket.ApplyTriage(priority, assigned?.Id, dueAt, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);
        result.Triage = new TriageDecision(priority, dueAt,
            SlaEvaluator.Evaluate(dueAt, null, now, triageOptions.AtRiskThresholdPercent, window),
            context.ForcesCriticalPriority ? "Category rule forces Critical priority." : request.RequestedPriority is null ? "Defaulted to Medium priority." : "Used the requested priority.",
            $"{window:0.##} hour response window{(context.IsPremium ? " after the Premium customer reduction and minimum floor" : "") }.", assignmentReason);
        return result;
    }
}
