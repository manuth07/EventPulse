\# Test Plan — Sprint 1



\## Scope

Event Service (GET /api/events, GET /api/events/{id}) and Identity Service

(register, verify-email, resend-verification) — based on live OpenAPI

contracts pulled from dev branch.



\## In Scope

\- API-level functional testing (Postman) against dev branch

\- Pre-merge testing of feature/EP-8-customer-registration

\- Dependency/security vulnerability scanning

\- Unit test coverage verification

\- Performance testing (JMeter) — planned

\- E2E testing (Selenium) — planned



\## Out of Scope

\- Booking Service, Payment Service (no business endpoints built yet this sprint)

\- Event Create/Update/Delete (not yet implemented — GET only so far)



\## Test Environment

\- Local Docker Compose (Postgres on 5433)

\- Gateway :7000, Identity :7101, Event :7102, Booking :7103, Payment :7104

\- Branch: dev (baseline), feature/EP-8-customer-registration (pre-merge)



\## Tools

Postman, dotnet test + coverlet, Apache JMeter, Selenium + NUnit,

dotnet list --vulnerable, npm audit



\## Entry Criteria

\- All services pass /health check

\- OpenAPI contract available for service under test



\## Exit Criteria

\- All 25 baseline test cases executed with recorded results

\- All P1/Critical bugs filed

\- Coverage and security scan reports generated

