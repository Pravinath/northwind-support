using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(int id, [FromServices] GetTicketQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it when no agent is given.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    [HttpPost("{id:int}/escalate")]
    public Task<EscalationResult> Escalate(int id, EscalateTicketRequest request,
        [FromServices] EscalateTicketCommandHandler handler, CancellationToken ct) => handler.HandleAsync(id, request, ct);

    [HttpGet("{id:int}/escalations")]
    public async Task<IReadOnlyList<TicketEscalationDto>> Escalations(int id,
        [FromServices] ITicketRepository tickets, [FromServices] ITicketEscalationRepository history, CancellationToken ct)
    {
        if (await tickets.GetByIdAsync(id, ct) is null) throw new SupportDesk.Application.Exceptions.NotFoundException("Ticket", id);
        return (await history.GetForTicketAsync(id, ct)).Select(x => new TicketEscalationDto(x.Id, x.FromPriority,
            x.ToPriority, x.FromAgentId, x.ToAgentId, x.FromDueAtUtc, x.ToDueAtUtc, x.Reason, x.EscalatedBy, x.EscalatedAtUtc)).ToArray();
    }
}
