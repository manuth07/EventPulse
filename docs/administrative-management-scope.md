# EventPulse Administrative Management Scope & Policy Specification

**Document Reference:** `EP-368`  
**Parent Epic / Feature:** `EP-57` / `US-37` — Administrative Management  
**Target Services:** Identity Service (`eventpulse_id`), Event Service (`eventpulse_ev`), Booking Service (`eventpulse_bk`), API Gateway (`YARP`), Frontend (`React`)  
**Audience:** Software Engineers, System Architects, QA Automation Engineers, Product Owner, Security Auditors  
**Status:** Defined Scope & Policy Specification (Awaiting Product Owner Baseline Approval)  
**Date:** October 2026 (Sprint 4)

---

## 1. Executive Summary & Objectives

User Story **US-37 (EP-57)** specifies the administrative governance capabilities required by platform administrators to supervise user accounts, maintain platform integrity, enforce access control, and protect platform security across EventPulse.

The objective of **EP-368** is to formally establish the scope, permissions matrix, boundaries, prohibited actions, data visibility rules, validation constraints, and audit logging standards for Administrative Management. This specification ensures that subsequent implementation tasks (backend endpoints, security filters, data transfer objects, frontend management tables, and state updates) adhere to strict least-privilege principles, protect sensitive data, and maintain clear separation of concerns across EventPulse microservices.

---

## 2. Current Implementation Analysis (Sprint 1–3 Baseline)

Prior to Sprint 4 (EP-57), platform administrative functionality was distributed across specific functional workflows. An inspection of the codebase confirmed the following existing baseline:

| Service / Area | Existing Administrative Functionality | Implementation Details & Gaps |
| :--- | :--- | :--- |
| **Identity Service** | • **Organizer Application Review:** `OrganizerApplicationsController.cs`<br>• `GET /api/admin/organizer-applications` (list pending/all applications)<br>• `POST /api/admin/organizer-applications/{id}/approve` (grants Organizer role)<br>• `POST /api/admin/organizer-applications/{id}/reject` (rejection with reviewer notes)<br>• **Role Definition:** `AppRoles.cs` defines `Customer`, `Organizer`, `Administrator`<br>• **Admin Bootstrap:** `IdentityDataSeeder.cs` provisions seed administrator if configured.<br>• **Active Flag:** `ApplicationUser.IsActive` exists; enforced in `LoginService` and `GoogleAuthService`. | • **No User Listing Endpoint:** No administrative API to query, paginate, or search platform users.<br>• **No User Management API:** No endpoints to view user details, change roles, or toggle `IsActive`.<br>• `UsersController.cs` only exposes `/api/users/me/*` endpoints for self-service profile management. |
| **Event Service** | • **Event Approvals & Rejections:** `EventsController.cs`<br>• `GET /api/events/pending`<br>• `POST /api/events/{id}/approve`<br>• `POST /api/events/{id}/reject`<br>• **Modification Requests:** Approve/reject event cancellation and update requests.<br>• **Admin Notifications:** `AdminNotificationsController.cs` (EP-151 / EP-32) for unread event submission alerts. | • Strictly scoped to event lifecycle management.<br>• Does not possess user identity records. |
| **YARP API Gateway** | • Routes `/api/admin/organizer-applications/{**catch-all}` to `identity_cluster`.<br>• Routes `/api/events/pending` and review routes to `event_cluster`. | • Does not have generic administrative user management route `/api/admin/users/{**catch-all}` configured. |
| **Frontend Application** | • `AdminDashboard.jsx`: Central hub with links to Organizer Applications and Pending Events, plus notification polling badge.<br>• `AdminOrganizerApplications.jsx`: Table and review modal for organizer requests.<br>• `PendingEvents.jsx`: Table and review modal for event submissions. | • Dashboard contains a static card for *"Platform Security & Role Governance"*, but has no navigation or interface for user administration. |

---

## 3. Microservice Ownership & System Architecture

EventPulse follows a decentralized microservices architecture using ASP.NET Core and PostgreSQL. Administrative user management strictly respects service domain boundaries:

```
                      ┌────────────────────────────────────────┐
                      │        React Frontend (Admin)          │
                      └───────────────────┬────────────────────┘
                                          │
                                   [YARP Gateway]
                       /api/admin/users/* │ /api/admin/metrics
                                          ▼
                      ┌────────────────────────────────────────┐
                      │    Identity Service (eventpulse_id)    │
                      │  - ApplicationUser (ASP.NET Identity)  │
                      │  - IdentityRole (AppRoles)             │
                      │  - User Management Controller (EP-369) │
                      └───────┬──────────────────────┬─────────┘
                              │                      │
             Inter-Service /  │ HTTP / Query API     │ HTTP / Query API
             Client Query     ▼                      ▼
           ┌────────────────────────┐      ┌─────────────────────────┐
           │     Event Service      │      │     Booking Service     │
           │    (eventpulse_ev)     │      │     (eventpulse_bk)     │
           │   - Total Events       │      │   - Total Bookings      │
           └────────────────────────┘      └─────────────────────────┘
```

### 3.1 Data Item Ownership Matrix

| Data Item | Description | Authoritative Owner | Storage / DB | Access Protocol |
| :--- | :--- | :--- | :--- | :--- |
| **User Identity & Roles** | User ID, Name, Email, Roles (`Customer`, `Organizer`, `Administrator`), `IsActive`, `CreatedAt`, `EmailConfirmed`, `CountryCode`, `PhoneNumber`, `ProfilePictureUrl` | **Identity Service** | PostgreSQL (`eventpulse_id`) | Direct Entity via `UserManager<ApplicationUser>` |
| **Organizer Applications** | Verification status, Business name, Tax ID, Review comments | **Identity Service** | PostgreSQL (`eventpulse_id`) | `IOrganizerApplicationService` |
| **Event Aggregate Count** | Total registered events (Draft, Published, Completed, Cancelled) | **Event Service** | PostgreSQL (`eventpulse_ev`) | Read-only metrics endpoint or aggregate service |
| **Booking Aggregate Count**| Total ticket orders and reservations | **Booking Service** | PostgreSQL (`eventpulse_bk`) | Read-only metrics endpoint or aggregate service |
| **Audit Logs** | Historical records of administrative changes | **Identity Service** / Audit Store | PostgreSQL / Structured Logs | Dedicated audit repository / Serilog sink |

---

## 4. Admin-Visible Information Specification

To comply with privacy laws (GDPR, Sri Lanka Personal Data Protection Act No. 9 of 2022) and least-privilege security design, administrative access to user data must be strictly compartmentalized.

### 4.1 Permitted Viewable Information

Administrators are permitted to inspect only operational identity attributes:

1. **User ID:** Platform-wide UUID / GUID (`ApplicationUser.Id`).
2. **User Name:** Full display name derived from `FirstName` and `LastName`.
3. **Email Address:** Canonical, normalized email address (`ApplicationUser.Email`).
4. **Assigned Roles:** Current role collection (`Customer`, `Organizer`, `Administrator`).
5. **Account Status:** 
   - Active status: `IsActive` (Active vs Deactivated/Suspended).
   - Verification status: `EmailConfirmed` (Verified vs Unverified).
   - Lockout status: `LockoutEnd` (Locked out due to excessive failed attempts vs Normal).
6. **Registration Date:** UTC timestamp when the user registered (`ApplicationUser.CreatedAt`).
7. **Basic User Profile Details:**
   - Phone number: `PhoneNumber`
   - Country code: `CountryCode` (ISO 3166-1 alpha-2)
   - Profile completion flag: `ProfileCompleted`
   - Avatar URL: `ProfilePictureUrl`
8. **Platform Overview Metrics (Dashboard Aggregates):**
   - Total registered users count (segmented by role: Customers, Organizers, Admins).
   - Total published/active events count (retrieved from Event Service).
   - Total bookings count (retrieved from Booking Service).

### 4.2 Prohibited Information (Strict Data Shielding)

The backend MUST NEVER project, serialize, or transmit the following sensitive fields in any administrative DTO or response:

| Prohibited Data Item | Field / Location | Reason for Exclusion |
| :--- | :--- | :--- |
| **Password Hashes** | `ApplicationUser.PasswordHash` | Zero-knowledge security. Administrators have no legitimate need to see hash strings. |
| **Security Stamps** | `ApplicationUser.SecurityStamp`, `ConcurrencyStamp` | Internal cryptographic tokens used for session validation and optimistic concurrency. |
| **Password Reset Tokens** | `PasswordResetToken` table | Exposure allows account takeover by unauthorized administrators or token leakage. |
| **Email Verification OTPs** | `EmailVerificationCode` table | Sensitive authentication challenge codes. |
| **Payment Credentials** | Stripe Customer IDs, Payment Intent secrets, card numbers | Controlled exclusively by Stripe PCI-DSS compliance in Payment Service. |
| **Raw Authentication Secrets** | Google OAuth tokens, Refresh tokens | Strictly ephemeral to the client authentication handshake. |

---

## 5. Permitted Administrative Management Actions

The following administrative actions are in scope for **EP-57 / US-37**:

### 5.1 Action 1: View Registered Users (Paginated List)
- **Description:** Retrieve a paginated collection of registered user accounts.
- **Requirements:**
  - Enforce pagination limits (default page size: 10 or 20; maximum: 100).
  - Return metadata: `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`.
  - Include summary fields for each user (ID, Name, Email, Roles, `IsActive`, `EmailConfirmed`, `CreatedAt`).

### 5.2 Action 2: Search and Filter Users
- **Description:** Locate specific accounts or cohorts of users efficiently.
- **Permitted Query Filters:**
  - **Search Query (`q`):** Case-insensitive prefix/substring match on `Email`, `FirstName`, `LastName`.
  - **Role Filter (`role`):** Filter by specific role (`All`, `Customer`, `Organizer`, `Administrator`).
  - **Status Filter (`status`):** Filter by account status (`All`, `Active`, `Inactive`, `Unverified`).
  - **Sorting (`sortBy`, `sortDirection`):** Sort by `CreatedAt` (default descending), `LastName`, `Email`.

### 5.3 Action 3: View User Details
- **Description:** Inspect the full operational record of an individual user account.
- **Route:** `GET /api/admin/users/{userId}`
- **Payload:** Comprehensive safe DTO containing profile information, current roles, security status, and summary of past organizer applications (if any).

### 5.4 Action 4: Activate User Account
- **Description:** Restore an inactive or suspended user account to active status.
- **Route:** `POST /api/admin/users/{userId}/activate` (or `PUT /api/admin/users/{userId}/status`)
- **Effects:**
  - Sets `ApplicationUser.IsActive = true`.
  - Clears any temporary identity lockout (`LockoutEnd = null`, `AccessFailedCount = 0`).
  - Permits the user to authenticate successfully.
  - Emits an administrative audit event.

### 5.5 Action 5: Deactivate User Account
- **Description:** Suspend a user account to immediately prevent platform access.
- **Route:** `POST /api/admin/users/{userId}/deactivate` (or `PUT /api/admin/users/{userId}/status`)
- **Effects:**
  - Sets `ApplicationUser.IsActive = false`.
  - Updates `SecurityStamp` to immediately invalidate existing JWT refresh tokens and active sessions.
  - Subsequent login attempts are rejected with `ACCOUNT_DISABLED` / 403 Forbidden.
  - Existing bookings and created events remain in the database for referential integrity (events may be handled via Event Cancellation policies if required).
  - Emits an administrative audit event.

### 5.6 Action 6: Change User Role
- **Description:** Promote or demote a user account between system roles.
- **Route:** `PUT /api/admin/users/{userId}/roles` (or `POST /api/admin/users/{userId}/role`)
- **Supported Transitions:**
  - Promote `Customer` → `Organizer` (manual administrative grant bypassing application).
  - Promote `Customer` or `Organizer` → `Administrator`.
  - Demote `Organizer` → `Customer`.
  - Demote `Administrator` → `Organizer` or `Customer` (subject to safety invariants).
- **Effects:**
  - Atomically adds/removes roles via `UserManager<ApplicationUser>`.
  - Updates user's `SecurityStamp` so subsequent JWT refresh reflects the updated role claims.
  - Emits an administrative audit event with previous role and new role.

---

## 6. Prohibited Actions & Safety Invariants

To prevent catastrophic administrative errors, privilege escalation, and platform lockouts, the system MUST enforce the following inviolable rules at the backend service layer:

### Invariant 1: Self-Deactivation Prohibition
> **Rule:** An administrator CANNOT deactivate their own account.
- **Technical Enforcement:**  
  The endpoint must compare the authenticated caller's identity (`User.FindFirst(ClaimTypes.NameIdentifier)`) with the target `userId`.  
  If `callerId == targetUserId`, reject immediately with HTTP 400 Bad Request (`SELF_DEACTIVATION_PROHIBITED`: *"Administrators cannot deactivate their own account."*).

### Invariant 2: Last Active Administrator Guard
> **Rule:** An administrator CANNOT deactivate or demote the last remaining active Administrator in the system.
- **Technical Enforcement:**  
  Before deactivating an Administrator or removing the `Administrator` role from any user, query:
  $$\text{ActiveAdminCount} = \text{Count}(\text{Users in role 'Administrator' where } \text{IsActive} == \text{true})$$
  If $\text{ActiveAdminCount} \le 1$ and the target user is an active Administrator, the operation MUST be blocked with HTTP 400 Bad Request (`LAST_ADMIN_PROTECTION`: *"Cannot deactivate or demote the last remaining active Administrator on the platform."*).

### Invariant 3: Self-Role Demotion Guard
> **Rule:** An administrator CANNOT remove the `Administrator` role from their own account.
- **Technical Enforcement:**  
  If `callerId == targetUserId` and the target role change removes `Administrator`, reject with HTTP 400 Bad Request (`SELF_DEMOTION_PROHIBITED`: *"Administrators cannot remove administrative privileges from their own account. Another administrator must perform this action."*).

### Invariant 4: No Permanent Hard Deletion (Soft Suspension Only)
> **Rule:** Permanent physical deletion (`DELETE FROM "AspNetUsers"`) is strictly PROHIBITED in EP-57.
- **Rationale:**  
  EventPulse users have foreign key dependencies across events (`OrganizerId`), ticket bookings (`CustomerId`), organizer applications, and payment records. Hard-deleting user rows causes database constraint violations or cascades that destroy audit history and financial records.
- **Enforcement:**  
  No `DELETE /api/admin/users/{id}` endpoint shall be exposed. All account suspensions must be executed via `IsActive = false`.

### Invariant 5: Credential Impersonation & Secret Inspection Prohibition
> **Rule:** Administrators CANNOT reset another user's password directly to a plaintext value or view any credentials.
- **Enforcement:**  
  Password resets must always follow the self-service cryptographic email OTP/token workflow (EP-26 / EP-352). Administrators cannot view or generate arbitrary password tokens on behalf of users.

### Invariant 6: Payment Credential & Secret Access Prohibition
> **Rule:** User administration APIs have zero coupling with Stripe secret credentials or customer payment details.

---

## 7. Validation Rules & Request Schemas

All administrative endpoints must strictly validate payloads and query parameters using standard EventPulse validation conventions:

### 7.1 User List & Filter Query Parameters
```
GET /api/admin/users?page={page}&pageSize={pageSize}&q={q}&role={role}&status={status}&sortBy={sortBy}&sortDesc={sortDesc}
```
- `page`: Integer $\ge 1$ (default: `1`).
- `pageSize`: Integer between $1$ and $100$ (default: `20`). If $> 100$, clamp to `100` or return validation error.
- `q`: Optional string, trimmed, maximum length: 100 characters. Sanitize against SQL wildcard injection.
- `role`: Optional string. Permitted values: `All`, `Customer`, `Organizer`, `Administrator` (case-insensitive).
- `status`: Optional string. Permitted values: `All`, `Active`, `Inactive`, `Unverified` (case-insensitive).
- `sortBy`: Permitted values: `CreatedAt`, `LastName`, `Email` (default: `CreatedAt`).
- `sortDesc`: Boolean (default: `true`).

### 7.2 Role Update Request Schema
```json
{
  "roles": ["Organizer"]
}
```
- `roles`: Non-empty array of valid role strings.
- Values must match `AppRoles.Customer`, `AppRoles.Organizer`, or `AppRoles.Administrator`.
- Must not contain unknown role strings (`INVALID_ROLE_SPECIFIED`).

### 7.3 Account Status Update Request Schema
```json
{
  "isActive": false,
  "reason": "Suspicious fraudulent activity reported on ticket bookings."
}
```
- `isActive`: Boolean required.
- `reason`: Optional string, maximum length: 500 characters, useful for audit logging.

---

## 8. Authorization Requirements & Gateway Configuration

### 8.1 Backend Policy Enforcement
- Microservice endpoints under user management must require authenticated JWT tokens with the `Administrator` role claim.
- Attribute decorator:
  ```csharp
  [Authorize(Policy = "AdministratorOnly")]
  ```
  *(Policy configured in IdentityService `Program.cs` via `options.AddPolicy("AdministratorOnly", policy => policy.RequireRole(AppRoles.Administrator));`)*
- Calls without valid JWT: `401 Unauthorized`.
- Calls with JWT belonging to `Customer` or `Organizer`: `403 Forbidden`.

### 8.2 YARP Gateway Route Integration
To expose administrative user endpoints to the frontend, `backend/gateway/src/EventPulse.Gateway/appsettings.json` must include a dedicated route:
```json
"identity-admin-users-route": {
  "ClusterId": "identity_cluster",
  "CorsPolicy": "AllowFrontend",
  "Match": { "Path": "/api/admin/users/{**catch-all}" }
}
```

---

## 9. Audit Logging Requirements

Every administrative action modifies user state or roles and must produce a structured, non-repudiable audit log entry:

### 9.1 Required Audit Log Fields
1. **Timestamp:** UTC ISO 8601 timestamp.
2. **Actor ID (`AdminUserId`):** GUID of the administrator performing the action.
3. **Actor Email:** Email of the administrator.
4. **Target User ID (`TargetUserId`):** GUID of the affected user.
5. **Target User Email:** Email of the affected user.
6. **Action:** Exact action identifier:
   - `ADMIN_USER_ACTIVATED`
   - `ADMIN_USER_DEACTIVATED`
   - `ADMIN_USER_ROLE_CHANGED`
7. **Previous State:** Serialized snapshot of relevant fields before change (`{ "isActive": true, "roles": ["Customer"] }`).
8. **New State:** Serialized snapshot of relevant fields after change (`{ "isActive": false, "roles": ["Organizer"] }`).
9. **Reason / Notes:** Administrator-provided reason string (if supplied).
10. **Client IP Address & User Agent:** Captured from HTTP request context.
11. **Outcome:** `SUCCESS` or `FAILURE` (with error message if failed).

### 9.2 Logging Standards
- Audit logs must be emitted via Serilog with high priority and structured metadata (`LogInformation("AUDIT: Admin {AdminId} performed {Action} on User {TargetUserId}...", ...)`).
- Audit log messages must never contain passwords, tokens, or personal financial details.

---

## 10. Scope Boundaries & Out-of-Scope Features

To prevent architectural overlap and maintain single-responsibility boundaries across Sprint 4 tasks, the following features are explicitly categorized as **OUT OF SCOPE** for EP-57:

| Feature / Capability | Respective Epic / User Story | Managing Service | Status / Boundary |
| :--- | :--- | :--- | :--- |
| **Event Approval & Rejection** | `EP-30`, `EP-31`, `EP-32`, `EP-151` | `EventService` | Already implemented in `EventsController.cs` and `AdminNotificationsController.cs`. Do not duplicate. |
| **Organizer Application Review** | `EP-28`, `EP-29` | `IdentityService` | Already implemented in `OrganizerApplicationsController.cs`. Linked via UI, but separate workflow. |
| **Dynamic Reports Generation** | Separate Reporting Epic | Reporting / BI Service | Out of scope for user administration. |
| **Sales & Booking Analytics** | Analytics Epic / User Story | Analytics / Event Service | Out of scope for user administration. |
| **Payment & Refund Management** | `EP-54`, `EP-55` (`EP-365`) | `PaymentService` | Managed via Payment Service Stripe refund workflows. |
| **Permanent Hard Deletion (GDPR Right to be Forgotten)** | Future Compliance Epic | Cross-Service Choreography | Hard deletion requires distributed saga/event choreography across bookings, tickets, and payments. Out of scope for EP-57. |

---

## 11. Decisions Requiring Product Owner Confirmation

The following business policy options are highlighted for formal sign-off by the Product Owner before final UI/UX implementation:

| Decision Item | Proposed Baseline | Alternative Option | Engineering Recommendation |
| :--- | :--- | :--- | :--- |
| **1. Role Cardinality** | **Single Role Assignment**<br>A user is either a `Customer`, an `Organizer`, or an `Administrator`. | **Multiple Concurrent Roles**<br>A user can possess `Customer` and `Organizer` simultaneously. | **Single Role:** ASP.NET Core Identity supports multiple, but EventPulse UI and permissions treat `Organizer` as a superset of `Customer` (Organizers can also attend events). Single active role simplifies governance. |
| **2. Deactivation Email Notification** | **No Automated Email (Silent Suspension)**<br>Immediate login block without email. | **Automated Suspension Email**<br>Send notification explaining account suspension. | **Silent Suspension initially:** Brevo SMTP in `IdentityService` currently only handles registration OTP and password reset. Email notification can be added once notification service is centralized. |
| **3. Immediate Token Revocation** | **SecurityStamp Regeneration**<br>Invalidates existing refresh tokens. Existing JWT expires within 15–60 minutes. | **Distributed Blacklist**<br>Redis token blacklist checking every request. | **SecurityStamp Regeneration:** Does not introduce Redis dependency; aligns with existing stateless JWT architecture. |
| **4. Account Deactivation Reason** | **Optional Reason String**<br>Admin can optionally provide context. | **Mandatory Predefined Reason Code**<br>Admin must select from dropdown (e.g. `FRAUD`, `SPAM`, `VIOLATION`). | **Optional Reason String** provides flexibility for initial release. |

---

## 12. Verification & Traceability Matrix

| Requirement ID | Specification Clause | Implementation Task | Verification Strategy |
| :--- | :--- | :--- | :--- |
| **REQ-ADM-01** | Section 4.1: View registered users list | EP-369 (Backend) / EP-370 (Frontend) | Integration test querying paginated user list |
| **REQ-ADM-02** | Section 4.1: Search and filter users | EP-369 (Backend) / EP-370 (Frontend) | Unit & integration tests filtering by role and status |
| **REQ-ADM-03** | Section 5.4 / 5.5: Activate and deactivate accounts | EP-369 (Backend) / EP-371 (Frontend) | Integration tests verifying `IsActive` toggle and login rejection |
| **REQ-ADM-04** | Section 5.6: Change user roles | EP-369 (Backend) / EP-371 (Frontend) | Tests promoting Customer to Organizer and Administrator |
| **REQ-ADM-05** | Section 6: Prohibited actions & safety guards | EP-369 (Backend) | Unit tests verifying self-deactivation and last-admin blocks |
| **REQ-ADM-06** | Section 4.2: Sensitive data protection | EP-369 (Backend DTOs) | Serialization tests verifying exclusion of PasswordHash and tokens |
| **REQ-ADM-07** | Section 9: Structured audit logging | EP-369 (Backend) | Test log verification for administrative actions |
