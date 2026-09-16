# EventPulse Development Guidelines

---

## 1. Purpose

These guidelines exist to maintain architectural consistency, protect working functionality, and streamline collaboration across a team utilizing AI-assisted and human development ("vibe coding").

As the codebase scales, these guidelines ensure that all contributors:
- Maintain consistency across features and services.
- Prevent duplicated, parallel, or contradictory implementations.
- Eliminate spaghetti code and premature abstractions.
- Protect established user flows and working system behavior.
- Make ongoing maintenance, debugging, and onboarding straightforward.
- Preserve the existing microservice boundaries, contracts, and deployment architecture.

> **Core Philosophy:**  
> **“These are guardrails, not blockers. Developers may introduce new patterns when genuinely required, but they should first confirm that the existing project does not already provide an appropriate solution.”**

---

## 2. General Development Principles

- **Understand before modifying:** Inspect related existing code, endpoints, models, and UI flows before touching a single line.
- **Extend existing patterns:** Prefer extending an existing controller, service, DTO, or component over introducing parallel mechanisms.
- **Keep changes scoped:** Confine your modifications strictly to the current user story or task.
- **Avoid unnecessary refactoring:** Never refactor working, unrelated features while delivering a feature ticket.
- **Avoid speculative abstractions:** Write clean, direct code for today's concrete requirements. Do not over-engineer for hypothetical futures.
- **Respect working implementations:** Do not rewrite working systems simply because another personal style or library seems marginally cleaner.
- **Prioritize readability and testability:** Code is read far more often than it is written. Use clear naming, concise methods, and obvious logic.
- **Focused responsibilities:** Keep classes, controllers, services, and React components focused on a single responsibility.
- **Remove dead code safely:** Remove truly dead code when you refactor, but never leave temporary hacks or commented-out blocks without clear explanation.
- **No duplicated business rules:** Centralize domain rules (e.g., ticket price formatting, role validation, application statuses) in their canonical domain layer.
- **Eliminate magic values:** Use shared enums, constants, or configuration keys rather than repeated raw strings or magic numbers.
- **Respect established boundaries:** Keep frontend code in `frontend/`, gateway configuration in `backend/gateway/`, and service code strictly within `backend/services/<service-name>/`.

---

## 3. Change Safety & Existing Feature Protection

Existing working features must remain fully operational. Before modifying existing code:
- **Map dependencies:** Identify all upstream callers (controllers, frontend services, background consumers) and downstream dependencies.
- **Preserve established contracts:** Keep API route paths, request payloads, response structures, and HTTP status codes backward-compatible unless the user story explicitly mandates a contract change.
- **Protect critical paths:** Never break authentication, token issuance, role-based authorization, organizer application submission, administrator event review, image uploads, YARP gateway routing, or deployment pipeline configurations.
- **Synchronize full-stack contract changes:** When an API contract must change, update the backend endpoint, DTOs, tests, and the frontend service layer together.
- **Verify immediately:** Run relevant automated unit/integration tests and verify affected UI screens before declaring work complete.

> **Cardinal Rule:**  
> **Do not perform broad “cleanup” or project-wide rewrites while implementing a normal user story unless explicitly requested.**

---

## 4. Backend Guidelines

EventPulse microservices are built on **ASP.NET Core (.NET 10)**, **Entity Framework Core**, and **PostgreSQL**.

- **EF Core as the Standard:** Use EF Core for data access. Do not introduce raw ADO.NET, Dapper, or direct SQL strings when EF Core is the project standard, unless high-throughput bulk processing explicitly demands it.
- **Slim Controllers:** Controllers (`Controllers/`) should handle HTTP concerns only—model binding, authentication claim extraction, model state validation, delegating to the service layer, and returning proper `ActionResult` responses.
- **Service Layer for Business Logic:** Place domain validation, status transitions, and data orchestration inside dedicated service classes (e.g., `IEventSubmissionService`, `IOrganizerApplicationService`).
- **DTOs at API Boundaries:** Never expose EF Core entities directly to API callers. Use dedicated DTOs for requests (`DTOs/Create*Request.cs`) and responses (`DTOs/*Dto.cs`).
- **Separation of Concerns:** Keep persistence models (`Models/`) separate from API contracts (`DTOs/`) and presentation logic.
- **Async All the Way:** Use asynchronous I/O (`async`/`await`) for all database operations, external HTTP calls, and blob storage access.
- **Cancellation Tokens:** Accept and forward `CancellationToken` in controller action signatures and down into asynchronous EF Core methods (e.g., `ToListAsync(cancellationToken)`).
- **Dependency Injection:** Register services with their interfaces (`IServiceCollection.AddScoped<I..., ...>()`) following existing `Program.cs` conventions.
- **Server-Side Authorization:** Never rely on client claims or client-provided IDs for authorization. Extract user identity and roles server-side directly from validated JWT claims (`User.FindFirst(ClaimTypes.NameIdentifier)` or `sub`).
- **Predictable HTTP Status Codes:**
  - `200 OK` / `201 Created` for successful operations.
  - `400 Bad Request` with structured error messages for validation failures.
  - `401 Unauthorized` when authentication token is missing or invalid.
  - `403 Forbidden` when the authenticated user lacks the required role or ownership.
  - `404 Not Found` when a requested entity does not exist.
  - `409 Conflict` for duplicate unique records (e.g., duplicate email registration).
- **Strong Typing over Raw Strings:** Use domain enums (e.g., `EventStatus.cs`, `ApplicationStatus.cs`) with string conversion in EF Core rather than raw string comparisons.
- **Database Integrity:** Enforce data constraints (unique indexes, string lengths, foreign keys) in the database via EF Core Fluent API (`OnModelCreating`), not merely in request validation.

---

## 5. Microservice Boundaries

EventPulse maintains strict domain separation across its services. Each service owns its dedicated PostgreSQL database schema:

```
                      ┌──────────────────────────────┐
                      │    YARP API Gateway (:7000)   │
                      └──────────────┬───────────────┘
                                     │
         ┌───────────────────┬───────┴───────────┬───────────────────┐
         ▼                   ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Identity Service│ │  Event Service  │ │ Booking Service │ │ Payment Service │
│     (:7101)     │ │     (:7102)     │ │     (:7103)     │ │     (:7104)     │
└─────────────────┘ └─────────────────┘ └─────────────────┘ └─────────────────┘
```

### Service Responsibilities

1. **Identity Service (`backend/services/identity-service`)**
   - User authentication, registration, password hashing, and JWT issuance.
   - Google OAuth ID token verification.
   - Email verification with 6-digit OTPs.
   - Role assignments (`Customer`, `Organizer`, `Administrator`).
   - Customer profile management (`/api/users/me/profile`).
   - Organizer application submission, resubmission, and admin review (`/api/organizer-applications`, `/api/admin/organizer-applications`).

2. **Event Service (`backend/services/event-service`)**
   - Public event discovery and search (`/api/events`, `/api/events/{id}`).
   - Organizer event submission, editing, and resubmission (`/api/events`, `/api/events/{id}/resubmit`).
   - Administrator event review (approve/reject with notes) (`/api/events/admin/pending`, `/api/events/{id}/approve`, `/api/events/{id}/reject`).
   - Event poster and cover banner storage via blob storage (`IEventImageStorage`).
   - Ticket categories and base pricing for events.

3. **Booking Service (`backend/services/booking-service`)**
   - Cart management, ticket inventory holds, and temporary reservations.
   - Booking records, booking status tracking, and reference codes.
   - Confirmation and ticket record generation.

4. **Payment Service (`backend/services/payment-service`)**
   - Payment gateway integration (Stripe test mode).
   - Checkout session initiation, payment verification, and webhook handling.
   - Payment transaction audit logging.

5. **API Gateway (`backend/gateway`)**
   - Reverse proxy routing using Microsoft YARP.
   - Single point of entry for frontend and public clients (`http://localhost:7000` locally, Azure App Service in production).
   - Route mapping, path prefixing, and health-check aggregation.
   - **Zero business logic** lives in the gateway.

### Cross-Service Rules
- **No Direct Database Access:** Never connect to or query another microservice's database directly. No cross-service database joins.
- **Decoupled Interaction:** Cross-service communication must occur via HTTP client calls or asynchronous Kafka events.
- **Preserve Placements:** Do not relocate features or entities between microservices casually. Follow the established boundaries.

---

## 6. Kafka / Event-Driven Architecture

EventPulse utilizes **Apache Kafka** for asynchronous, cross-service event propagation where loose coupling is required.

- **Pragmatic Usage:** Use Kafka for meaningful cross-service lifecycle events (e.g., `OrderPlaced`, `PaymentCompleted`, `TicketIssued`), **not** for ordinary synchronous CRUD operations.
- **Synchronous CRUD Remains HTTP:** Querying events, viewing organizer applications, or logging in must remain direct HTTP REST endpoints via the Gateway.
- **Clear Event Contracts:** Place shared event definitions in `backend/shared/contracts/kafka/`. Use explicit, versionable contract schemas.
- **Producer/Consumer Decoupling:** Producers publish domain events without knowledge of consumer implementation details.
- **Idempotent Consumers:** Network partitions and retries can cause duplicate message delivery. Consumers should be designed to handle duplicate events safely.
- **Architectural Approval:** Do not introduce new Kafka topics or consumers into a feature unless the user story or architecture requires asynchronous event-driven handling.

---

## 7. Database Guidelines

- **Database Engine:** PostgreSQL.
- **Service Ownership:** Each service connects exclusively to its designated database (e.g., `eventpulse_identity`, `eventpulse_events`).
- **Migrations for All Schema Changes:** Never manually modify database tables in production. All schema evolution must be managed via EF Core Migrations.
- **Migration Workflow:**
  1. Update model entities (`Models/`) and Fluent API configuration in `Data/*DbContext.cs`.
  2. Generate a new migration using `dotnet ef migrations add <DescriptiveName>`.
  3. Review the generated migration C# file to verify columns, types, indexes, and nullability.
  4. Test locally using `dotnet ef database update`.
  5. Ensure existing records are preserved or safely migrated with default values.
- **Currency & Money Representation:**
  - Money and price values must strictly use `decimal` in C# and `decimal(18,2)` (or `numeric`) in PostgreSQL.
  - Never use `float` or `double` for currency.
- **Whole-LKR Ticketing Scope:** In accordance with EventPulse's Sri Lankan event ticketing specifications, event ticket prices are whole Sri Lankan Rupee (LKR) amounts without cents (e.g., `LKR 1,500`, `LKR 0`).
- **Data Integrity Constraints:**
  - Define max lengths on string columns (e.g., `HasMaxLength(200)`).
  - Use unique indexes for unique business constraints (e.g., normalized email, active booking references).
  - Use cascade rules cautiously to avoid accidental data loss.

---

## 8. API Design

- **Predictable REST Routes:** Follow existing URL conventions:
  - Collection: `GET /api/events`, `POST /api/events`
  - Resource: `GET /api/events/{id}`, `PUT /api/events/{id}`
  - Nested sub-resources: `GET /api/events/my-submissions`
  - Action endpoints: `POST /api/events/{id}/approve`, `POST /api/events/{id}/reject`
- **Request/Response DTOs:** All request payloads and response bodies must use dedicated DTO classes with explicit validation attributes (`[Required]`, `[MaxLength]`, `[EmailAddress]`).
- **Meaningful Validation Responses:** Return structured error objects:
  ```json
  {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed.",
    "errors": ["Title is required.", "Price cannot be negative."]
  }
  ```
- **JWT Identity over Request IDs:** When an endpoint performs an action on behalf of the current user, derive the user's `Guid` from the authenticated JWT claims. Never trust a `userId` supplied in the request body or query string.
- **Pagination:** For endpoints returning potentially unbounded collections, implement pagination (`page`, `pageSize`) to safeguard performance.

---

## 9. Authentication & Authorization

- **Unified JWT System:** All authenticated endpoints validate JWT Bearer tokens issued by the `IdentityService`.
- **Dual Sign-In:** EventPulse supports both standard Email/Password authentication (with email OTP verification) and Google OAuth 2.0 (Google Identity Services).
- **Backend Enforcement:** Frontend navigation guards and conditional button rendering are strictly for User Experience. **All security and access checks must be enforced in backend controllers.**
- **Role-Based Authorization:**
  - Public endpoints: anonymous access permitted (e.g., `GET /api/events`).
  - Customer endpoints: `[Authorize]` (authenticated users).
  - Organizer endpoints: `[Authorize(Policy = AppPolicies.OrganizerOnly)]` (requires `Organizer` role).
  - Admin endpoints: `[Authorize(Policy = AppPolicies.AdminOnly)]` (requires `Administrator` role).
- **Ownership Verification:** Before an Organizer can view or update a submission (e.g., `/api/events/my-submissions/{id}`), verify that `event.OrganizerId == authenticatedUserId`.
- **Zero Secrets in Code:** Never commit JWT signing keys, passwords, client secrets, or connection strings to source control. Use environment variables and local development user secrets.

---

## 10. Frontend Guidelines

The frontend is a modern Single Page Application built with **React 19**, **Vite 5**, and **React Router v7**.

- **Reuse Existing Components:** Check `frontend/src/components/` before creating a new component. Common widgets like `Header`, `Hero`, `EventCard`, `EventSkeleton`, `EmptyState`, `ErrorState`, and `RouteGuards` already exist.
- **Component Placement:**
  - Route/page views live in `frontend/src/pages/<PageName>/<PageName>.jsx`.
  - Shared reusable components live in `frontend/src/components/<ComponentName>/<ComponentName>.jsx`.
- **Centralized API Service Layer:**
  - Never make raw `fetch` or `axios` calls directly inside React page components.
  - Encapsulate all backend communication in `frontend/src/services/` (`authService.js`, `eventService.js`, `organizerApplicationService.js`).
  - All services must resolve the backend endpoint through the centralized configuration in `frontend/src/services/apiConfig.js` via `getApiBaseUrl()`.
- **Complete UI States:** Every screen that loads dynamic data must handle:
  1. `loading` state (render skeletons or spinners, e.g., `<EventSkeleton />`).
  2. `error` state (render clear error messages and retry actions, e.g., `<ErrorState />`).
  3. `empty` state (render a friendly fallback when no records exist, e.g., `<EmptyState />`).
  4. `success` state (render the populated UI).
- **Centralized Auth Context:** Consume user state and token access through `useAuth()` from `frontend/src/context/AuthContext.jsx`. Do not roll custom token storage listeners.
- **Route Guarding:** Protect private client routes using `<RequireRole allowedRoles="..." />` in `App.jsx`.

---

## 11. UI & Design System

The visual design source of truth is documented in detail in:
📁 [`frontend/styleguide.md`](file:///e:/CSP%20project/EventPulse/frontend/styleguide.md)

### Design Core Rules
- **Brand Identity:** The EventPulse aesthetic is:
  > **Apple-inspired restraint + EventPulse orange identity + modern ticket marketplace**
- **Color Palette:**
  - Primary Accent: `--ep-primary` (`#FF5B00`)
  - Primary Hover: `--ep-primary-hover` (`#E05000`)
  - Soft Accent: `--ep-soft-accent` (`#FFF0E6`)
  - Light Canvas: `--ep-canvas` (`#F5F5F7`)
  - White Cards: `--ep-card-bg` (`#FFFFFF`)
  - Neutral Borders: `--ep-border` (`#E5E5EA`)
  - Dark Typography: `--ep-text-primary` (`#1D1D1F`)
  - Muted Typography: `--ep-text-secondary` (`#86868B`)
  - **No Unrelated Colors:** Do not introduce arbitrary purple, neon blue, or random bright accents.
- **Design Tokens:** Always consume CSS variables from `frontend/src/index.css`:
  - Card radius: `var(--ep-radius-card)` (16px)
  - Button radius: `var(--ep-radius-btn)` (12px)
  - Badge radius: `var(--ep-radius-badge)` (8px)
  - Focus ring: `var(--ep-focus-ring)`
  - Card shadow: `var(--ep-shadow-card)`
- **Typography:**
  - Headings: `'Plus Jakarta Sans'`, sans-serif (`var(--ep-font-heading)`)
  - Body: `'Inter'`, sans-serif (`var(--ep-font-body)`)
- **Price Formatting:** Always format ticket prices using `formatPrice()` from `frontend/src/utils/currencyFormatter.js` to adhere to the whole-LKR convention (e.g., `LKR 2,500`).
- **Responsive Behavior:** Ensure interfaces adapt gracefully between desktop, tablet, and mobile breakpoints using Bootstrap grid utilities and responsive flex layouts.

---

## 12. Avoiding Duplicate & Spaghetti Code

Before writing new functionality:
- **Search the repository first:** Search for existing endpoints, utility functions, validation logic, and UI components.
- **Extend existing files:** If an existing service or component is 80% of what you need, extend it with optional parameters or sub-functions rather than creating a duplicate.
- **Avoid parallel clients:** Never create a second API helper or fetch wrapper when `apiConfig.js` and existing services exist.
- **Avoid scattered business rules:** Keep status checks (e.g., whether an application is approved or an event can be edited) inside domain services, not duplicated across 5 different controllers and 4 React components.
- **Balanced Abstraction:** Do not create abstract generic factories or helper layers to save 2 lines of obvious code. Code clarity beats DRY dogma when over-abstraction obscures flow.

---

## 13. Error Handling

- **No Raw Exceptions to Clients:** Never let unhandled exceptions or stack traces leak to HTTP clients in production. Use standard ASP.NET Core exception handling middleware.
- **Consistent API Error Responses:** Return JSON error payloads containing at least `code` and `message`:
  ```csharp
  return BadRequest(new { code = "INVALID_OPERATION", message = "This event cannot be resubmitted." });
  ```
- **Frontend Graceful Degradation:**
  - Catch API errors in service calls and present user-friendly error messages in the UI.
  - Never leave a page stuck in infinite loading when an API call fails. Always set `loading: false` in `finally` blocks.
  - Log technical error details to browser `console.error` for developer debugging.

---

## 14. Logging

- **Use Structured Logging:** Inject `ILogger<T>` into backend controllers and services. Use structured log templates:
  ```csharp
  _logger.LogInformation("Event submission created with ID {EventId} for organizer {OrganizerId}", eventItem.Id, organizerId);
  ```
- **No `Console.WriteLine`:** Avoid `Console.WriteLine` for standard backend application logging.
- **Never Log Sensitive Information:**
  - ❌ Do NOT log: Passwords, JWT tokens, OTP verification codes, Google credentials, Stripe secret keys, credit card details, or database connection strings.
  - ✔️ DO log: Entity IDs, user IDs (GUIDs), operation outcomes, exception types, and execution timestamps.

---

## 15. Testing

- **Backend Test Frameworks:**
  - Test framework: `xUnit`
  - Mocking: `Moq`
  - In-Memory Testing: `Microsoft.EntityFrameworkCore.InMemory`
- **What to Test:**
  - Domain business rules (e.g., status transitions, role checks).
  - Validation failures (e.g., negative prices, missing titles).
  - Authorization edge cases (e.g., organizer attempting to edit another organizer's event).
  - Both happy paths and expected error paths.
- **Pre-Completion Checklist:**
  1. Backend builds cleanly (`dotnet build backend/EventPulse.Backend.sln`).
  2. All backend unit tests pass (`dotnet test backend/EventPulse.Backend.sln`).
  3. Frontend builds cleanly (`npm run build` in `frontend/`).
  4. Manual end-to-end sanity check of the primary user flow.

---

## 16. Git & Pull Request Guidelines

- **Branch Naming:** Use descriptive branch names with Jira/ticket prefix where applicable:
  - `feat/EP-123-ticket-tiers`
  - `fix/EP-179-cd-reliability`
- **Focused Commits:** Keep commits focused on a single logical change with clear, imperative commit messages (e.g., `feat(event-service): add event cover banner support`).
- **Zero Secrets in Git:** Verify `.gitignore` prevents staging `.env`, `.env.*` (except `.env.example`), connection strings, and build output directories (`bin/`, `obj/`, `dist/`).
- **Clean Diff Review:** Always review `git diff` before committing to ensure no unintended scratch files or formatting noise are included.
- **Careful Conflict Resolution:** When merging branches, understand incoming changes rather than blindly overwriting a teammate's commits.

---

## 17. Configuration & Secrets

EventPulse follows standard 12-factor configuration principles:

| Environment | Mechanism |
| :--- | :--- |
| **Local Development** | `appsettings.Development.json`, .NET User Secrets, `.env.development.local` (git-ignored) |
| **Production (Azure)** | Azure App Service Environment Variables (`Google__ClientId`, `ConnectionStrings__*`), GitHub Actions Secrets & Variables |

### Strict Rules
- Never hardcode:
  - PostgreSQL connection strings or passwords.
  - JWT signing keys (`Jwt__Key`).
  - Brevo SMTP/API credentials.
  - Google OAuth Client Secrets.
  - Stripe Secret Keys or Webhook Secrets.
  - Azure deployment tokens or service principal credentials.
- All configuration keys in `appsettings.json` and `.env.example` must contain only empty or dummy placeholder values.

---

## 18. Dependency Management

- **Assess before adding:** Before running `npm install <package>` or `dotnet add package <package>`, verify whether ASP.NET Core, React, or standard library APIs already provide the necessary capability.
- **Avoid Micro-Utilities:** Do not add external packages for trivial functions (e.g., simple string formatting or date parsing).
- **Compatibility:** Verify that any new package is compatible with **.NET 10** and **React 19**.
- **Document Rationale:** Note in the Pull Request why a newly introduced third-party package was necessary.

---

## 19. Performance & Scalability

- **Prevent N+1 Queries:** Use EF Core `.Include()` eagerly when related entities are needed, or project directly into DTOs via `.Select()` to fetch only required columns.
- **Read-Only Optimization:** Use `.AsNoTracking()` on EF Core queries for read-only GET endpoints to bypass entity change-tracking overhead.
- **Asynchronous I/O:** Always use async methods (`SaveChangesAsync`, `FirstOrDefaultAsync`, `ReadAsStringAsync`).
- **Asset Optimization:** Store large media (event posters, banners) in Azure Blob Storage / cloud storage, returning URLs to clients. Never store raw image byte arrays in relational databases.
- **Avoid Premature Optimization:** Write clean, expressive code first. Profile and optimize when concrete latency or memory bottlenecks are identified.

---

## 20. Definition of Ready (DoR) for Development

A user story is ready for implementation when:
- [ ] Requirements and user expectations are clearly described.
- [ ] Acceptance criteria (AC) are explicit, unambiguous, and testable.
- [ ] Service boundaries and affected microservices are identified.
- [ ] Impact on API contracts, database schema, and UI screens is understood.
- [ ] Any required external assets or mock designs are available.
- [ ] Story has been estimated and broken down into actionable tasks.

---

## 21. Definition of Done (DoD) Alignment

A user story is complete when:
- [ ] All Acceptance Criteria are fully implemented.
- [ ] Solution compiles cleanly with zero build errors.
- [ ] Automated tests for new business rules pass.
- [ ] Existing automated test suite runs without regressions.
- [ ] UI adheres to `frontend/styleguide.md` and design tokens.
- [ ] Manual verification confirms the feature functions properly through the API Gateway.
- [ ] Code is formatted, clean diff reviewed, and committed to git.
- [ ] Jira story-specific DoD requirements are satisfied.

---

## 22. AI-Assisted Development Rules

When working with AI coding assistants (e.g., Antigravity, GitHub Copilot, Cursor):

### Before Generating Code
1. **Inspect First:** Read existing files, models, controllers, and components to observe the prevailing code style.
2. **Search for Precedents:** Search the codebase for similar existing features before drafting new implementations.
3. **Verify Signatures:** Confirm that referenced classes, DTOs, route paths, and utility methods actually exist in the repository.

### During Implementation
- **Minimal Touch:** Modify only the minimal necessary set of files required for the story.
- **Preserve Established Patterns:** Do not introduce a second dependency injection style, a different state management library, or an alternate CSS methodology.
- **No Silent Replacements:** Never delete or replace existing working functions unless the task explicitly calls for an update.
- **No Fabricated APIs:** Never invent non-existent backend endpoints or database columns without implementing the corresponding backend changes.

### After Implementation
- **Review Diff:** Check `git diff` to ensure no unintended edits, syntax errors, or debug code were introduced.
- **Compile and Test:** Run the build and test suites to verify that the generated code functions as expected.

> **AI Conflict Rule:**  
> **“If the requested implementation conflicts with the existing architecture or would require a significant architectural change, stop and explain the conflict before making the architectural change.”**

---

## 23. When Refactoring Is Appropriate

Refactoring is encouraged when:
- It directly supports the current user story by making the target code safer and easier to extend.
- Duplicate business logic is causing real maintenance overhead.
- Tight coupling prevents writing adequate unit tests.
- It was explicitly requested as a refactoring task.

Refactoring should:
- Maintain full behavioral equivalence for all existing callers.
- Be backed by existing or new unit tests.
- Remain strictly scoped—do not expand a localized fix into a solution-wide rewrite.

---

## 24. EventPulse-Specific Flow Protection

The core EventPulse business lifecycle follows a structured progression across services. New features must integrate cleanly into this sequence without bypassing stages:

```
[1. Customer Registration & Verification] (Identity Service)
  │  Customer registers with name, email, password, phone, country.
  │  6-digit OTP email verification via Brevo SMTP.
  │  Customer authenticates via Password or Google OAuth.
  ▼
[2. Organizer Application] (Identity Service)
  │  Customer submits organization details, contact, and website.
  │  Status: Pending.
  ▼
[3. Administrator Organizer Review] (Identity Service)
  │  Administrator reviews application.
  │  Approve: User granted "Organizer" role.
  │  Reject: Notes provided; Organizer may resubmit.
  ▼
[4. Organizer Event Creation & Submission] (Event Service)
  │  Organizer submits event title, venue, category, date, whole-LKR price,
  │  and uploads poster and cover banner images.
  │  Status: Pending.
  ▼
[5. Administrator Event Review] (Event Service)
  │  Administrator reviews pending event details and artwork.
  │  Approve: Event status -> "Approved" / "Published".
  │  Reject: Review comment provided; Organizer may edit and resubmit.
  ▼
[6. Public Event Discovery] (Event Service & Gateway)
  │  Approved events are listed publicly on homepage and event details screens.
  │  Customers search and filter published events.
  ▼
[7. Ticket Selection & Reservation] (Booking Service)
  │  Customer selects tickets; temporary hold/cart created.
  │  Availability and inventory validated.
  ▼
[8. Payment Processing] (Payment Service)
  │  Customer completes checkout via Stripe (test mode).
  │  Webhook confirms successful transaction.
  ▼
[9. Booking Confirmation & Ticket Issuance] (Booking Service)
  │  Booking marked confirmed.
  │  Unique booking reference and ticket details generated.
```

> **Flow Protection Rule:**  
> Features must not circumvent these stages (e.g., an event must never be published without Administrator approval; an organizer cannot publish an event without having the approved `Organizer` role).

---

## 25. Final Principle

> **“Consistency is preferred over novelty. Simplicity is preferred over unnecessary abstraction. Existing working behavior should be preserved unless the requirement explicitly changes it. These guidelines are intended to help developers move faster with confidence, not prevent reasonable engineering decisions.”**
