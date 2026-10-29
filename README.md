# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle. It uses a .NET 10 API, React 19 / TypeScript, and SQL Server.

## Assignment status

| Task | Work completed |
| --- | --- |
| 1. Defect and server-side filtering | Fixed stale ticket list requests, added regression coverage, and applies filters before counting and paging. |
| 2. Ticket triage and escalation | Automatic priority/assignment/SLA decisions, escalation audit history, and SLA filtering in the UI and API. |
| 3. Login and authentication | Agent login with hashed passwords and JWTs, protected API and web routes, and authenticated escalation identity. |
| 4. Dockerise the API | Multi-stage API image and Compose API service on port 5080, with SQL readiness dependency and runtime configuration. |

This is an assessment implementation. Registration, password reset, roles, OAuth/SSO, refresh tokens,
MFA, notifications, CI and caching are outside scope.

## Design decisions

- **DEFECT-117:** The ticket list request effect previously missed non-search filter changes. A later
  search change then made the stale selection appear to take effect. Non-search filter changes now
  fetch immediately; search remains debounced. API filters are composed in the EF query before
  count and paging.
- **Triage and SLA:** Application handlers apply policy using category flags and customer tier.
  Response windows, Premium adjustment and thresholds are configurable. SLA state is derived from
  timestamps and the injected clock instead of being stored and becoming stale. Assignment chooses
  an eligible, least-loaded agent, with agent ID as a stable tie-break; if none qualifies, ticket
  creation succeeds unassigned with an explanation.
- **Escalation:** A non-terminal ticket moves up one priority level, receives a recalculated due
  date, and records a separate audit row in `TicketEscalations`. The composite index
  `IX_TicketEscalations_TicketId_EscalatedAtUtc` supports looking up one ticket's history in
  newest-first order without sorting the full table. Actor identity comes from the authenticated
  JWT, never from a user-supplied request field.
- **Authentication:** Password hashes are stored on the existing Agent row. ASP.NET Identity's
  `PasswordHasher<Agent>` provides salted, versioned password hashing; plaintext passwords are not
  stored. The API issues 60-minute JWTs and requires authentication through a global fallback
  authorization policy. Login and Swagger remain open. Invalid email/password responses are the
  same, and inactive agents cannot log in.
- **Browser token storage:** The access token is kept in `sessionStorage`, attached to API requests,
  and cleared on logout or any 401. This keeps the token for the current tab session without
  persisting it across browser restarts. JavaScript can read session storage, so an XSS vulnerability
  could expose the token; production would need a reviewed token-storage and CSP strategy.
- **Docker:** The image builds from the repository root, restores before copying source for layer
  caching, and runs as the non-root runtime user on port 8080. Compose exposes it at localhost:5080
  and waits for SQL Server health. Configuration and signing keys are supplied at runtime. Compose
  uses Development mode for this assessment so startup migrations and demo seeding run on an empty
  database. For production, use a controlled migration job and production secrets; do not enable
  development seeding or commit real credentials.

### Alternatives considered

- **Filtering:** Client-only filtering was rejected because it would only filter the currently loaded
  page and produce incorrect totals. Applying filters in the API query keeps results and pagination
  correct for every client.
- **Agent credentials:** A separate user table was rejected for this assessment because Agents
  already represent the people assigned to tickets. A nullable password hash on Agent reuses that
  identity without introducing a second account lifecycle.
- **Password handling:** Plaintext and reversible encryption were rejected; a salted password hash
  is appropriate because the application only needs to verify a password, never recover it.
- **SLA state:** Persisting a separate SLA status was rejected because it could drift from the due
  date and resolution timestamps. Deriving it keeps display and filtering consistent.
- **Token storage:** `localStorage` was rejected because it persists across browser sessions.
  `sessionStorage` limits persistence to the tab session, while its JavaScript access remains a
  documented XSS trade-off.
- **Migrations in Docker:** Automatically migrating every production API instance was rejected.
  Compose uses Development mode for this local assessment; a real deployment should run a
  controlled migration step before serving traffic.
- **Patterns:** Use-case handlers keep ticket workflows independently testable, repository
  interfaces isolate data access from application rules, and triage/SLA options keep policy values
  configurable. These seams are used where the code needs them rather than adding a new framework.

## Run locally

Prerequisites: .NET SDK 10, Node.js compatible with the repository, and Docker Desktop.

```powershell
Copy-Item .env.example .env
npm ci
npm run db:up
npm run api
```

In a second terminal:

```powershell
npm run web
```

Open http://localhost:5173. The app redirects to `/login` first. Local demo credentials:

- Email: `alex.turner@northwind-support.example`
- Password: `Northwind-Local-Only-2026`

The same fixture password is assigned to active seeded agents. The seeded inactive agent cannot
sign in. Do not reuse the development password or signing key outside this local demo.

### Run with the API container

```powershell
Copy-Item .env.example .env
docker compose up --build
```

The API is available at http://localhost:5080 and its database health endpoint at
http://localhost:5080/health. In another terminal, point Vite at it:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'
npm run web
```

Compose waits for SQL Server health before starting the API. In Development mode, the API applies
EF migrations and seeds the demo records on startup. Stopping the stack with `docker compose down`
preserves the named SQL volume. `docker compose down -v` deletes that database volume and its data.

### Configuration

The checked-in appsettings files contain local-only examples. For Docker Compose, `.env` values
provide the SQL password, JWT signing key and demo password. The important API environment settings
are `ConnectionStrings__Default`, `Jwt__SigningKey`, and `DevAuth__SeedPassword`; double underscores
map environment variables to nested .NET configuration keys. Never commit a real `.env` or
production secret. JWT issuer, audience and lifetime are non-secret settings in `appsettings.json`;
the development lifetime is 60 minutes. The Docker build excludes `appsettings.Development.json`
so its local connection string and fixture credentials are not copied into the image.

## Tests and checks

```powershell
dotnet test SupportDesk.sln
npm test --workspace apps/web
npm run lint --workspace apps/web
npm run typecheck --workspace apps/web
npm run build --workspace apps/web
docker compose config -q
```

Backend unit and architecture tests do not need a database. During this assessment run, the backend
suite passed 70 tests (66 unit and 4 architecture), and the web suite passed 15 tests; lint,
typecheck and build passed. The API solution build also passed. NuGet reports existing moderate and
high advisories for `System.Security.Cryptography.Pkcs` 9.0.11; this assessment did not change that
transitive dependency. `docker compose config -q` and `docker compose build api` passed. The
container started after SQL Server became healthy; `/health`, login, and `/api/auth/me` all worked
through `http://localhost:5080`.

Manual checks:

1. Open `/login`, sign in, then verify the agent name and Logout control.
2. In DevTools Network, confirm API calls carry an Authorization Bearer header.
3. Sign out; protected routes should redirect to login. A request without a token returns 401.
4. Create an Outage ticket and confirm forced Critical priority and automatic triage. Create a
   non-Critical Billing ticket, escalate it, and inspect the priority and history.
5. Filter tickets by SLA status and confirm the request query contains `slaStatus=...`.
6. For the container, check `GET /health`, sign in, and confirm the web proxy's API request returns
   200. The container was also checked to run as a non-root user and to exclude the development
   settings file.

The running container returned `Healthy`; authenticated `GET /api/tickets` returned 200 with 42
seeded tickets, and `docker compose exec -T api whoami` returned `app`. `docker image ls` listed
`northwind-support-api:latest` at 457 MB. The SDK build stage, tagged `northwind-sdk-stage:check`,
was 2.43 GB. A separate Compose project on alternate ports and a fresh, project-scoped volume
returned `Healthy` and 40 seeded tickets; that temporary stack and volume were removed without
affecting the existing database. The source-change rebuild check for restore-layer reuse is still
passed in a temporary project copy: after adding a harmless C# comment, the second build showed the
`dotnet restore` step as `CACHED` while recopying source and publishing again. The first build
attempt in that copy hit a transient Docker DNS lookup error; its retry completed successfully.

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | Exchange agent email/password for a JWT. |
| `GET /api/auth/me` | Return the authenticated agent; otherwise 401. |
| `GET /api/tickets` | Paged list with search and status, priority, category, customer, assignee, unassigned, SLA and sort filters. |
| `GET /api/tickets/{id}` | One ticket. |
| `POST /api/tickets` | Create and automatically triage a ticket. |
| `POST /api/tickets/{id}/escalate` | Escalate with a reason; actor is taken from the JWT. |
| `GET /api/tickets/{id}/escalations` | Newest-first escalation history. |
| `PATCH /api/tickets/{id}/status` and `/assignment` | Change status or assignee. |
| `GET /api/customers`, `/api/agents`, `/api/categories` | Reference data. |
| `GET /health` | Anonymous database reachability check; returns 200 or 503. |

## AI use and known limitations

I used OpenAI Codex to clarify requirements and trace the ticket-list defect. I reviewed suggested
changes against the code, then checked them with tests and browser/API checks. I kept database-side
filtering because it preserves correct totals, and rejected client-side filtering because it only
covers the loaded page. The investigation found that non-search filters were missing from the
request effect; a regression test and Network check confirmed the fix. No AI-introduced issue was
found in these checks. I can explain the submitted implementation and design decisions in this
README and `APPROACH.md`.

- There is no registration, password reset, refresh token, MFA, or role/permission system.
- Logout clears this browser's session token but does not revoke a JWT already copied elsewhere;
  it remains valid until its 60-minute expiry.
- The Compose setup is for local assessment, not production. Production migration, secret
  management, TLS termination, rate limiting and browser security headers need deployment-specific
  configuration.
- The `System.Security.Cryptography.Pkcs` 9.0.11 advisory remains unresolved.

## Submission

Keep the requested task commits separate, update this README and `APPROACH.md`, and submit the link
to the personal fork, development credentials, and a truthful rough time breakdown for each task.
Use your actual time records for the breakdown; do not estimate hours after the fact as if they were
tracked. Reported task time: Task 1, 1.5 hours; Task 2, 4 hours; Task 3, 3 hours; Task 4, 1 hour;
total, 9.5 hours. Describe AI assistance accurately and be prepared to explain and defend the
implementation.
