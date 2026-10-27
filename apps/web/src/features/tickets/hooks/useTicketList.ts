import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);
  const [state, setState] = useState<State>({ isLoading: true });

  const {
    status,
    priority,
    categoryId,
    customerId,
    assignedAgentId,
    unassignedOnly,
    page,
    pageSize,
    sortBy,
    sortDirection,
  } = filters;

  const requestFilters = useMemo<TicketFilters>(
    () => ({
      search: debouncedSearch,
      status,
      priority,
      categoryId,
      customerId,
      assignedAgentId,
      unassignedOnly,
      page,
      pageSize,
      sortBy,
      sortDirection,
    }),
    [
      debouncedSearch,
      status,
      priority,
      categoryId,
      customerId,
      assignedAgentId,
      unassignedOnly,
      page,
      pageSize,
      sortBy,
      sortDirection,
    ],
  );

  const load = useCallback(async () => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(requestFilters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, [requestFilters]);

  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedSearch(filters.search), DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  useEffect(() => {
    void load();
  }, [load]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => {
    setFilters(emptyFilters);
    setDebouncedSearch(emptyFilters.search);
  };

  return { filters, updateFilters, resetFilters, reload: load, ...state };
}
