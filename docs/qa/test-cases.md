# EventPulse — Sprint 1 Test Case Matrix

Scope: Event Service (`localhost:7102`) + Identity Service (`localhost:7101`)
Based on live OpenAPI contracts pulled 2026-08-28.

Legend: **P** = Priority (P1 highest), **Sev** = Severity if it fails

\---

## 1\. Event Service — GET /api/events

|ID|Scenario|Preconditions|Steps|Expected Result|Actual|Status|Sev|P|
|-|-|-|-|-|-|-|-|-|
|TC-EVT-001|List all events (happy path)|Events seeded in DB|GET /api/events|200, JSON array of `EventListDto` (id, title, description, venue, eventDate, price, status)|||Med|P2|
|TC-EVT-002|Empty database|No events in DB|GET /api/events|200, `\\\\\\\[]` — not 404/500|||High|P2|
|TC-EVT-003|Response shape validation|Events exist|GET /api/events, inspect one item|Every field present, `status` is an integer enum, `price` is consistently typed|||Med|P3|
|TC-EVT-004|Public access check|—|GET /api/events with no Authorization header|Confirm intended behavior (200 if public, 401 if not) — flag to BA/Dev if unclear which is correct|||Med|P3|
|TC-EVT-005|Content-negotiation|—|GET /api/events with `Accept: text/json` and `Accept: application/json`|Both return valid matching JSON|||Low|P4|

## 2\. Event Service — GET /api/events/{id}

|ID|Scenario|Preconditions|Steps|Expected Result|Actual|Status|Sev|P|
|-|-|-|-|-|-|-|-|-|
|TC-EVT-006|Get existing event (happy path)|Valid event UUID exists|GET /api/events/{validId}|200, full `EventDetailsDto` including `organizerId`|||Med|P2|
|TC-EVT-007|Non-existent but valid UUID|UUID well-formed, not in DB|GET /api/events/{randomUuid}|404 Not Found|||High|P1|
|TC-EVT-008|Malformed id (not a UUID)|—|GET /api/events/abc123|400 Bad Request (not 500)|||High|P1|
|TC-EVT-009|Empty id segment|—|GET /api/events/|404 (routes to list) or appropriate error, not 500|||Med|P2|
|TC-EVT-010|`price` field type consistency|Event with decimal price exists|GET /api/events/{id}, inspect `price`|Confirm whether returned as number or numeric string — verify frontend can parse it either way; report to Dev if inconsistent across records|||Med|P2|
|TC-EVT-011|SQL-injection-style id|—|GET /api/events/' OR '1'='1|400, no SQL error leaked, no data exposure (relevant since backend uses raw ADO.NET/EF — confirm parameterization holds)|||Critical|P1|

## 3\. Event Service — GET /api/events/health

|ID|Scenario|Steps|Expected Result|Actual|Status|
|-|-|-|-|-|-|
|TC-EVT-012|Health check|GET /api/events/health|200 OK|||

\---

## 4\. Identity Service — POST /api/auth/register

Required fields: `firstName`, `lastName`, `phoneNumber`, `countryCode`, `email`, `password`
Constraints: firstName/lastName ≤100 chars · phoneNumber ≤20 chars · countryCode exactly 2 chars · email ≤256 chars · password ≥8 chars

|ID|Scenario|Steps|Expected Result|Actual|Status|Sev|P|
|-|-|-|-|-|-|-|-|
|TC-AUTH-001|Register with valid data (happy path)|POST with all valid fields|200, verification email triggered (check SMTP/log)|||High|P1|
|TC-AUTH-002|Missing required field (e.g. no `email`)|POST without `email`|400 with clear validation message naming the field|||High|P1|
|TC-AUTH-003|Password below minimum length|`password: "1234567"` (7 chars)|400, password validation error|||High|P1|
|TC-AUTH-004|Password at exact boundary|`password` exactly 8 chars|200, accepted|||Med|P2|
|TC-AUTH-005|firstName over max length|`firstName` = 101 chars|400, not silently truncated|||Med|P2|
|TC-AUTH-006|countryCode wrong length|`countryCode: "LKA"` (3 chars) or `"L"` (1 char)|400|||Med|P2|
|TC-AUTH-007|Invalid email format|`email: "not-an-email"`|400 — confirm format is actually validated, spec only shows maxLength, not a format check|||High|P1|
|TC-AUTH-008|Duplicate email registration|Register same email twice|2nd attempt returns 400/409, not 200 or 500; existing account not overwritten|||Critical|P1|
|TC-AUTH-009|SQL/script injection in name fields|`firstName: "<script>alert(1)</script>"` or `firstName: "'; DROP TABLE Users;--"`|Rejected or safely stored/escaped — no execution, no SQL error|||Critical|P1|
|TC-AUTH-010|Malformed JSON body|Send broken JSON|400, not 500|||Med|P2|
|TC-AUTH-011|Password stored securely|Register, then inspect DB directly|Password is hashed in `AspNetUsers` table, never plaintext|||Critical|P1|
|TC-AUTH-012|Phone number format|`phoneNumber` with letters/symbols e.g. `"abc-123!!"`|Confirm whether format is validated or only length — flag if no format check exists|||Med|P3|

## 5\. Identity Service — POST /api/auth/verify-email

Required: `email`, `code` (exactly 6 chars)

|ID|Scenario|Steps|Expected Result|Actual|Status|Sev|P|
|-|-|-|-|-|-|-|-|
|TC-AUTH-013|Verify with correct code (happy path)|Register, retrieve real code, POST verify|200, account marked verified|||High|P1|
|TC-AUTH-014|Wrong code|Valid email, incorrect 6-digit code|400/401, account remains unverified|||High|P1|
|TC-AUTH-015|Code wrong length|`code: "12345"` (5 chars) or `"1234567"` (7 chars)|400|||Med|P2|
|TC-AUTH-016|Expired code|Wait past expiry window (check dev for TTL), then verify|400 with "expired" message, not accepted|||High|P1|
|TC-AUTH-017|Already-verified account|Verify twice with same valid code|2nd attempt handled gracefully (400 or idempotent 200), not 500|||Med|P2|
|TC-AUTH-018|Non-existent email|`email` not registered, any code|400/404, no account state change, no user-enumeration leak in error message|||Med|P2|
|TC-AUTH-019|Code reuse across accounts|Use account A's code against account B's email|Rejected|||High|P1|
|TC-AUTH-020|Brute-force code guessing|Submit many wrong codes rapidly for same email|Confirm whether rate-limiting/lockout exists — flag if none|||High|P2|

## 6\. Identity Service — POST /api/auth/resend-verification

Required: `email`

|ID|Scenario|Steps|Expected Result|Actual|Status|Sev|P|
|-|-|-|-|-|-|-|-|
|TC-AUTH-021|Resend for valid unverified account (happy path)|POST with registered, unverified email|200, new code sent, old code invalidated (test old code no longer works)|||High|P1|
|TC-AUTH-022|Resend for already-verified account|POST with verified email|Handled gracefully — clear response, no duplicate/confusing email sent|||Med|P2|
|TC-AUTH-023|Resend for non-existent email|POST with unregistered email|400/404, generic message (avoid confirming which emails are registered — enumeration risk)|||Med|P2|
|TC-AUTH-024|Resend spam/rate-limit check|Call endpoint repeatedly in quick succession|Confirm whether rate-limited — flag if a user can trigger unlimited emails|||Med|P3|

## 7\. Identity Service — GET /api/auth/health

|ID|Scenario|Steps|Expected Result|Actual|Status|
|-|-|-|-|-|-|
|TC-AUTH-025|Health check|GET /api/auth/health|200 OK|||

\---

## Known Scope Notes (not bugs)

* Event Service currently only exposes **GET** endpoints (list, get-by-id, health). No Create/Update/Delete for Events yet — confirm with BA/Dev whether this is planned later in Sprint 1 or a future sprint, since the assignment requires 4 CRUD operations minimum.
* No authentication/authorization scheme is declared in either OpenAPI spec — confirm which endpoints are meant to be public vs protected before writing final auth test cases.

## Already-Logged Bugs (from environment setup)

|ID|Title|Severity|Priority|Status|
|-|-|-|-|-|
|BUG-01|Gateway/Event Service use Microsoft.OpenApi 2.0.0 — known high-severity CVE (GHSA-v5pm-xwqc-g5wc)|High|P2|Open|
|BUG-02|Event Service crashes on fresh setup — EF Core migration not applied, undocumented in README|High|P1|Resolved (workaround known, README fix pending)|



