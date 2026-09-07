\## 2026-08-28

\*\*What I did:\*\* 

Set up full local environment from a fresh clone. Verified

Postgres + all 5 backend services (Gateway, Identity, Event, Booking,

Payment) healthy, frontend running. Pulled OpenAPI contracts for Event

Service and Identity Service, wrote a test plan and 25 test cases covering

happy path, edge cases, error conditions, and security checks.



\*\*Blockers:\*\* 

Event Service failed on first run — "Events" table did not

exist. Root cause: EF Core migration not applied. Resolved by running

`dotnet ef database update`.



\*\*Decisions:\*\* 

Will execute the 25 test cases against dev first to

establish a baseline before testing the not-yet-merged

feature/EP-8-customer-registration branch separately.



\*\*Plan for tomorrow:\*\* 

Execute test cases in Postman against dev, record

actual results, file any new bugs found.

