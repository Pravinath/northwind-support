# Approach

## Task 1: defect and filtering

The ticket list controls changed their values, but the request effect did not depend on most
non-search filters. That made the list look stale until a later search change triggered a request.
The fix schedules a request for each non-search filter change and keeps the short debounce on
search. The API applies each supplied filter to the EF query before counting and paging. Regression
tests cover the request behavior and server-side result/count behavior.

## Task 2: triage and escalation

Workflow rules live in the application handler; ticket state transitions stay in the domain
aggregate. Triage reads category flags and customer tier rather than depending on category display
names. Assignment considers active agents below capacity, applies specialization where required,
and sorts by open-ticket count then agent ID. With no eligible agent, ticket creation still succeeds
unassigned and records an explanation.

SLA state is derived from the due date, resolution time, configured threshold and injected clock; it
is not stored. Filtering is done in the database before count and paging. Escalation raises priority
one step, recalculates the due date, checks whether the current owner remains eligible, and records
an audit row in the same save as the ticket update. The actor is taken from the authenticated user
claims after Task 3; the client no longer supplies an actor name.

Verification includes backend rules and boundary tests, frontend escalation and SLA badge tests, and
manual checks creating an Outage ticket, escalating a non-Critical Billing ticket, and inspecting
the `slaStatus` query parameter in DevTools Network.

## Task 3: login and authentication

I added a nullable `PasswordHash` column to the existing Agent table through an EF migration rather
than creating a separate user table. Agents are already the principals assigned to tickets, so this
keeps identity and assignment records aligned. Passwords are hashed using ASP.NET Identity's
`PasswordHasher<Agent>`; seeded passwords are hashed during seeding and existing agents with no
hash receive the configured development fixture. Existing hashes are not overwritten.

The login handler returns the same unauthorized result for an unknown email, incorrect password,
or inactive agent. The presentation layer issues a signed JWT with agent ID, name and email, issuer,
audience and configured expiry. A global fallback authorization policy protects existing endpoints;
the login route and Swagger are exceptions. `/api/auth/me` verifies the current identity. Startup
validates that a signing key and positive lifetime are configured. The web app stores the token in
`sessionStorage`, attaches it to requests, restores the signed-in agent through `/me`, preserves the
requested route when redirecting to login, and clears the token after logout or any 401.

Escalation identity now comes from the authenticated name claim, preventing a caller from claiming
to be someone else in the request body. The browser form therefore asks only for the escalation
reason.

Verification includes password-hasher and login-handler tests, auth UI/client tests, and a manual
check signing in as Alex Turner, opening `/api/auth/me`, and confirming an unauthenticated protected
endpoint returns 401. The local demo password is `Northwind-Local-Only-2026` and the token lifetime
is 60 minutes. `sessionStorage` limits persistence to a browser tab session, but remains readable
by JavaScript; a production deployment should assess XSS protections and token storage together.

## Task 4: Dockerise the API

The API Dockerfile uses the .NET 10 SDK image for restore/publish and the ASP.NET runtime image for
execution. Project and package metadata are copied before source so code-only changes can reuse the
restore layer. `.dockerignore` excludes Git metadata, environment files, build outputs, node modules,
the Development appsettings file, and the web app. The runtime listens on port 8080 as the image's
non-root user. Issuer, audience and SLA policy defaults are non-secret base settings; signing key,
database connection and seeded password are runtime environment settings.

Compose builds the API from the repository root, publishes host port 5080, injects the SQL connection
and JWT/development seed settings at runtime, and waits for SQL Server's health check. In assessment
Development mode, API startup applies migrations and seeds demo data. `/health` is anonymous and
checks whether the configured database can be reached. Production should use a separate migration
job and externally managed secrets instead of Development seeding.

Verification passed: `docker compose config -q`, `docker compose build api`, and `docker compose up
-d` succeeded. Compose waited for SQL Server health; the API returned `Healthy` from
`GET http://localhost:5080/health`, and login plus `/api/auth/me` worked through the container. To
use the web dev server with the container, set `VITE_API_PROXY_TARGET=http://localhost:5080` before
`npm run web`. Avoid `docker compose down -v` when you need to retain local database data.

## Overall verification

- `dotnet test SupportDesk.sln` (unit and architecture tests)
- `npm test --workspace apps/web`
- `npm run lint --workspace apps/web`
- `npm run typecheck --workspace apps/web`
- `npm run build --workspace apps/web`
- Browser flow checks and DevTools Network inspection

The known NuGet advisory for `System.Security.Cryptography.Pkcs` 9.0.11 is reported in the README;
it is not silently treated as fixed by this work.
