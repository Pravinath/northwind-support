# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### DEFECT-117 root cause and fix

Root cause: the ticket-list request effect depended only on search, page, and page size, so
changing another filter updated the controls without scheduling a request. A later search change
triggered a request with the latest filter state, making the earlier selection appear stale. The fix
requests immediately when a non-search filter changes and debounces only search input.

### Task 2: ticket triage, SLA and escalation

- New tickets receive priority, assignment and due date in the application command handler.
  Category flags decide forced Critical priority and specialist eligibility; category names are
  never embedded in the rules. Eligible agents must be active, below capacity and, when required,
  specialized. The least-loaded agent wins, with the lowest agent ID as a stable tie-break.
- Response windows, Premium multiplier, minimum window and at-risk threshold come from the
  `Sla` section in `appsettings.Development.json`. SLA status is derived from due/resolution
  timestamps and the injectable clock, never stored. At risk means 25% or less of the applicable
  response window remains, including the Premium adjustment.
- Escalation raises priority one level, recalculates the due date from escalation time, keeps an
  eligible current owner or reassigns, and writes a separate audit record. The history index starts
  with `TicketId` for per-ticket retrieval and includes `EscalatedAtUtc` for newest-first ordering.
  Deleting a ticket cascades to its audit rows; agent references are restricted to protect history.
- Until Task 3 adds authentication, the escalation form asks for the actor name and sends it as
  `escalatedBy`. This is a development placeholder, not verified identity.
- The SLA list filter runs with the other query filters before SQL count, sorting and paging. The
  ticket table and details share the same text-labelled SLA badge, including “At risk”.
- When no eligible agent exists, the ticket is still created unassigned with a human-readable
  explanation; intake does not fail because the team is at capacity.

#### Design decisions

- Keep triage policy in the application layer and ticket state transitions in the domain aggregate.
  This keeps category display names out of business rules and makes the rules testable without SQL.
- Store response windows and thresholds in configuration instead of hard-coding them. Derive SLA
  state from timestamps and the injected clock instead of storing a value that could become stale.
- Select an eligible agent by open-ticket count, then agent ID. The stable tie-break makes the
  result predictable. If nobody qualifies, accept the ticket as unassigned and explain why.
- Escalate exactly one priority step and reset the deadline from the escalation time. Keep a
  separate immutable history row so the current ticket state does not erase the before/after record.
- Until authentication is implemented, accept `escalatedBy` from the form. This is a development
  placeholder; it is not proof of identity. The request value must be replaced by the signed-in
  user's identity if Task 3 is completed.

#### Task 2 verification

- Backend unit tests cover configured SLA windows for each priority and tier, the minimum window,
  forced Critical priority, agent eligibility and tie-breaking, escalation state/history and
  rejected escalation cases, plus SLA boundary behavior.
- Frontend tests cover escalation form validation, conflict handling and SLA badge states.
- Manual browser check: create an Outage ticket and confirm Critical priority and automatic
  assignment; create a non-Critical Billing ticket, escalate it, and confirm priority/history; set
  an SLA filter and confirm the tickets request contains `slaStatus` in DevTools Network.
- The SQL filter is verified by the server-side query tests, which confirm the filtered result and
  count are calculated before paging.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj
npm test --workspace apps/web
npm run lint --workspace apps/web
npm run typecheck --workspace apps/web
```

The backend unit and architecture tests do not need a database. During this assessment run,
`SupportDesk.sln` passed with 66 tests (62 unit and 4 architecture); the web suite passed with 9
tests, and lint, typecheck and build completed successfully. NuGet emitted existing moderate and
high vulnerability advisories for `System.Security.Cryptography.Pkcs` 9.0.11.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with search, status, priority, category, customer, assignee, unassigned, SLA status and sort filters. Filters run before count and paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create and automatically triage a ticket; response includes decision reasons |
| `POST /api/tickets/{id}/escalate` | Escalate with required reason and actor; returns ticket and audit row |
| `GET /api/tickets/{id}/escalations` | Newest-first escalation history |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## AI use and known limitations

I used OpenAI Codex to interpret the requirements, inspect the code, investigate the filter defect,
and draft or review implementation and test changes. A workflow that helped was to compare each
acceptance criterion with the code, then verify the behavior with focused tests and browser checks.
I kept the application-command/domain-aggregate split after checking that it matched the existing
architecture, and expanded the test plan to include exact SLA boundaries and escalation rejection
cases. I reviewed the generated changes and ran the test suite; I know of no remaining defect
introduced by the AI assistance.

- Tasks 1 and 2 are implemented. Authentication (Task 3) and Dockerising the API (optional Task 4)
  are not implemented in this submission.
- There is no development login credential because authentication is not implemented. The
  escalation actor field is user-entered until a trusted identity is available.
- The SQL Server image and local development setup are intended for assessment, not production.
- The dependency advisory for `System.Security.Cryptography.Pkcs` 9.0.11 remains unresolved.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
