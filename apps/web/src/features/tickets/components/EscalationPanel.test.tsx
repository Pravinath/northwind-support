import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({ ticketsApi: {
  getEscalations: vi.fn(), escalate: vi.fn(), getTicket: vi.fn(),
} }));

const ticket: TicketDetail = {
  id: 8, reference: 'TCK-0008', title: 'Billing problem', description: 'Details go here.', status: 'InProgress', priority: 'High',
  customer: { id: 1, name: 'Contoso', email: 'support@example.com', phone: null, tier: 'Standard' },
  category: { id: 2, name: 'Billing', requiresSpecialist: true }, assignedAgent: null,
  createdAtUtc: '2026-10-27T10:00:00Z', updatedAtUtc: '2026-10-27T10:00:00Z', dueAtUtc: '2026-10-27T18:00:00Z', resolvedAtUtc: null, slaStatus: 'WithinSla',
};

describe('EscalationPanel', () => {
  beforeEach(() => vi.resetAllMocks());

  it('blocks a short reason before making an API request', async () => {
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
    render(<EscalationPanel ticket={ticket} onUpdated={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/reason/i), { target: { value: 'bad' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByText(/between 5 and 500 characters/i)).toBeInTheDocument();
    expect(ticketsApi.escalate).not.toHaveBeenCalled();
  });

  it('shows a server conflict and leaves the displayed ticket unchanged', async () => {
    const onUpdated = vi.fn();
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
    vi.mocked(ticketsApi.escalate).mockRejectedValue(new ApiError(409, 'Ticket is already Critical.'));
    render(<EscalationPanel ticket={ticket} onUpdated={onUpdated} />);
    fireEvent.change(screen.getByLabelText(/reason/i), { target: { value: 'Customer impact increased.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Ticket is already Critical.');
    expect(onUpdated).not.toHaveBeenCalled();
    await waitFor(() => expect(ticketsApi.escalate).toHaveBeenCalledTimes(1));
  });
});
