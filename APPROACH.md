# Approach

## Task 1: defect and filtering

The ticket list controls changed their values, but the request effect did not depend on most
non-search filters. That made the list look stale until a later search change triggered a request.
The fix schedules a request for each non-search filter change and keeps the short debounce on
search. The API applies each supplied filter to the EF query before counting and paging, so the
pager describes the filtered result set. The regression tests check both the request behavior and
server-side filtering.

## Task 2: triage and escalation

I kept workflow rules in the application command handler and ticket state changes in the domain
aggregate. Triage reads category flags and customer tier from the database and uses the existing
`Sla` configuration. This avoids coupling business behavior to category display names and allows
the response windows to change without editing code. Assignment uses active agents who are below
capacity, and applies category specializations only when the category requires one. Least workload
is the primary sort and agent ID provides a deterministic tie-break. No eligible agent is a valid
outcome, so ticket creation still succeeds with an explicit explanation.

SLA state is derived from the due date, resolution time, configured risk threshold and injectable
clock. It is not a database field. The SLA list filter uses the same timestamps and configured
windows in the database query before count and paging.

Escalation is one level at a time. The handler checks terminal states and Critical priority,
re-evaluates the current owner, resets the response deadline from escalation time, and stores an
audit row in the same unit-of-work save as the ticket update. The audit table has a composite
`TicketId, EscalatedAtUtc` index because the UI retrieves one ticket's records newest first.

Until Task 3 provides a signed-in user identity, the caller supplies `escalatedBy`. The UI asks for
that value, and the handler validates it; it must be replaced with the authenticated identity in
Task 3. This placeholder is not an authentication mechanism.

## Task 2 verification

- Backend: `dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj`
- Frontend: `npm test --workspace apps/web`
- Frontend checks: `npm run lint --workspace apps/web` and
  `npm run typecheck --workspace apps/web`
- In the browser, create an Outage ticket to see forced Critical priority and Premium timing, then
  create a non-Critical Billing ticket to exercise escalation and inspect the history on its detail
  page. Use the SLA status filter on `/tickets`; the matching `slaStatus` value appears in the
  Network tab request query string.
