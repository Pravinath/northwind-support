import { useEffect, useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface EscalationPanelProps { ticket: TicketDetail; onUpdated: (ticket: TicketDetail) => void }

export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [validation, setValidation] = useState<string>();
  const canEscalate = ticket.priority !== 'Critical' && !['Resolved', 'Closed'].includes(ticket.status);

  useEffect(() => {
    let cancelled = false;
    void ticketsApi.getEscalations(ticket.id).then((entries) => {
      if (!cancelled) setHistory(entries);
    }).catch((caught: unknown) => {
      if (!cancelled) setError(toErrorMessage(caught, 'Could not load escalation history.'));
    });
    return () => { cancelled = true; };
  }, [ticket.id]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const cleanReason = reason.trim();
    if (cleanReason.length < 5 || cleanReason.length > 500) {
      setValidation('Enter an escalation reason between 5 and 500 characters.'); return;
    }
    setValidation(undefined); setError(undefined); setPending(true);
    try {
      await ticketsApi.escalate(ticket.id, cleanReason);
      const [updated, entries] = await Promise.all([ticketsApi.getTicket(ticket.id), ticketsApi.getEscalations(ticket.id)]);
      onUpdated(updated); setHistory(entries); setReason('');
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 409
        ? toErrorMessage(caught, 'This ticket can no longer be escalated.')
        : toErrorMessage(caught, 'Could not escalate this ticket.'));
    } finally { setPending(false); }
  };

  return <section className="card">
    <h2>Escalation</h2>
    {canEscalate ? <form className="form" onSubmit={submit} noValidate>
      <div className="field">
        <label className="field__label" htmlFor="escalation-reason">Reason (5–500 characters)</label>
        <textarea id="escalation-reason" rows={3} maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} />
      </div>
      {validation && <p className="field__error" role="alert">{validation}</p>}
      <button className="button button--ghost" type="submit" disabled={pending}>{pending ? 'Escalating…' : 'Escalate ticket'}</button>
    </form> : <p>This ticket cannot be escalated in its current state.</p>}
    {error && <p className="field__error" role="alert">{error}</p>}
    <h3>History</h3>
    {history.length === 0 ? <p>No escalations recorded.</p> : <ol className="escalation-history">
      {history.map((entry) => <li key={entry.id}>
        <strong>{entry.fromPriority} → {entry.toPriority}</strong>
        <p>Agent: {entry.fromAgentId ?? 'Unassigned'} → {entry.toAgentId ?? 'Unassigned'}</p>
        <p>Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}</p>
        <p>{entry.reason}</p>
        <small>{entry.escalatedBy} · {formatDateTime(entry.escalatedAtUtc)}</small>
      </li>)}
    </ol>}
  </section>;
}
