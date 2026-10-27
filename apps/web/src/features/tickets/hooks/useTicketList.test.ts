import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('requests other filter changes immediately and debounces search without losing them', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });
    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', search: '' }),
    );

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Search changes are debounced, but keep the latest non-search filter values.
    expect(getTickets).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });

    expect(getTickets).toHaveBeenCalledTimes(3);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', status: 'InProgress', page: 1 }),
    );

    act(() => {
      result.current.updateFilters({ search: '' });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });

    expect(getTickets).toHaveBeenCalledTimes(4);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: '', status: 'InProgress' }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });
});
