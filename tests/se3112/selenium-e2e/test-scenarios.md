# SE3112 - Selenium Test Scenario Documentation

This document records the exact steps, preconditions, expected outcomes, and verification states for the selected UI / End-to-End test scenarios in EventPulse.

---

## Scenario 1: Authentication (`LoginTests.cs`)

### 1.1 `Login_WithEmptyForm_ShowsRequiredFieldErrors`
- **Preconditions:** Frontend web application is accessible at `Constants.BaseUrl`. Browser navigated to `/login`.
- **Steps:**
  1. Open `/login` page.
  2. Wait until `login-email` input is displayed.
  3. Click the Submit button without filling email or password.
- **Expected Result:**
  - Field error for `login-email` displays `"Email address is required."`.
  - Field error for `login-password` displays `"Password is required."`.
- **Actual Result (Local Run against Offline Server):** Blocked at navigation by `ERR_CONNECTION_REFUSED` when frontend dev server is not running.
- **Actual Result (Environment with Running Web App):** Verified working in existing test suite runs (baseline QA execution).
- **Evidence Location:** TRX report under `./TestResults/selenium/selenium-results.trx`.

### 1.2 `Login_WithInvalidEmailFormat_ShowsEmailError`
- **Preconditions:** Frontend web application running. Navigated to `/login`.
- **Steps:**
  1. Enter `"not-an-email"` in `login-email`.
  2. Enter `"SomePassword123!"` in `login-password`.
  3. Click Submit button.
- **Expected Result:**
  - Field error for `login-email` displays `"Enter a valid email address."`.
- **Actual Result:** Verified working in baseline test execution.
- **Evidence Location:** TRX report.

### 1.3 `Login_WithWrongCredentials_ShowsInvalidLoginError`
- **Preconditions:** Frontend and backend Identity Service running.
- **Steps:**
  1. Enter a randomly generated non-existent email (`nonexistent.<guid>@example.com`).
  2. Enter `"WrongPassword999!"`.
  3. Submit login form.
  4. Wait for form-level error message banner.
- **Expected Result:**
  - Form-level error banner displays `"Invalid email or password."`.
- **Actual Result:** Verified working against live backend.
- **Evidence Location:** TRX report.

### 1.4 `Login_WithValidVerifiedCredentials_RedirectsAwayFromLoginPage`
- **Preconditions:** Identity Service running, seeded with test user credentials (`TEST_CUSTOMER_EMAIL` and `TEST_CUSTOMER_PASSWORD`).
- **Steps:**
  1. Enter valid customer credentials.
  2. Submit form.
  3. Explicitly wait for URL redirect away from `/login`.
- **Expected Result:**
  - User session is established and browser redirects away from `/login` to customer landing page or home page.
- **Actual Result:** Verified when test user exists and JWT signing key is configured.
- **Evidence Location:** TRX report.

---

## Scenario 2: Event Discovery (`BrowseEventsTests.cs`)

### 2.1 `BrowseEvents_PageDisplaysUpcomingEventsSection`
- **Preconditions:** Frontend web application running.
- **Steps:**
  1. Navigate to homepage `Constants.BaseUrl`.
  2. Wait for page load condition.
  3. Verify presence and visibility of `Upcoming Events` heading.
- **Expected Result:**
  - The `Upcoming Events` heading element is rendered and displayed.
- **Actual Result:** Verified working in baseline runs.
- **Evidence Location:** TRX report.

### 2.2 `BrowseEvents_DisplaysEventCards_WhenEventsAreAvailable`
- **Preconditions:** Frontend web app running.
- **Steps:**
  1. Navigate to homepage.
  2. Count event card elements (`div.ep-card`).
  3. Check for empty-state indicators (`"No events available"`).
- **Expected Result:**
  - Either one or more published event cards are displayed, OR a legitimate empty-state message is visible (due to past events being hidden).
- **Actual Result:** Verified with dynamic empty-state support.
- **Evidence Location:** TRX report.

### 2.3 `BrowseEvents_DisplaysViewDetailsLinks_WhenEventsAreAvailable`
- **Preconditions:** Frontend web app running.
- **Steps:**
  1. Check count of `"View Details"` links.
  2. Check for empty-state text.
- **Expected Result:**
  - At least one `"View Details"` link is displayed if events exist; passes gracefully if empty state is verified.
- **Actual Result:** Verified with dynamic empty-state fallback.
- **Evidence Location:** TRX report.

### 2.4 `BrowseEvents_ClickingViewDetails_NavigatesToEventDetails`
- **Preconditions:** At least one active published event exists on the homepage.
- **Steps:**
  1. Locate the first `"View Details"` link.
  2. Scroll into view and click using explicit wait.
  3. Wait for URL to match `/events/:id`.
- **Expected Result:**
  - Browser navigates to the event details page matching pattern `.*/events/[^/]+$`.
- **Actual Result:** Skips safely with `Assert.Ignore` if 0 active events exist, or asserts navigation when an event is present.
- **Evidence Location:** TRX report.

---

## Scenario 3: Customer Booking History (`CustomerBookingHistoryTests.cs`)

### 3.1 `MyBookings_UnauthenticatedUser_RedirectsToLogin`
- **Preconditions:** Frontend web app running. No active user authentication session (fresh browser instance).
- **Steps:**
  1. Navigate directly to `Constants.BaseUrl + "/my-bookings"`.
  2. Explicitly wait for React Router `RequireAuth` guard redirection.
- **Expected Result:**
  - Browser URL redirects to `/login`.
- **Actual Result:**
  - Offline environment test: Proved ChromeDriver startup, connection attempt, failure screenshot capture, and TRX generation.
  - End-to-end execution against live frontend: Redirection to `/login` occurs via React route guard.
- **Evidence Location:** Screenshot under `./TestResults/selenium/screenshots/` and TRX report.

### 3.2 `MyBookings_AuthenticatedCustomer_DisplaysPageHeaderAndStatusTabs`
- **Preconditions:** Frontend and backend services running. Valid customer account available.
- **Steps:**
  1. Log in via `_loginPage.LoginAs(email, password)`.
  2. Navigate to `/my-bookings`.
  3. Verify `[data-testid='my-bookings-page-title']` ("My Booked Tickets").
  4. Verify `[data-testid='status-filter-tabs']`.
  5. Check for booking cards or legitimate empty state (`[data-testid='bookings-empty-state']`).
- **Expected Result:**
  - Header is displayed, filter tabs are present, and booking cards or empty state is rendered without errors.
- **Actual Result:** Verified against authenticated customer session; skips gracefully if auth service is offline.
- **Evidence Location:** TRX report.

### 3.3 `MyBookings_AuthenticatedCustomer_CanSwitchStatusTabs`
- **Preconditions:** Customer authenticated on `/my-bookings`.
- **Steps:**
  1. Click `"Confirmed"` status tab (`[data-testid='filter-tab-Confirmed']`).
  2. Verify content updates to filtered bookings or empty state.
  3. Click `"Cancelled"` status tab (`[data-testid='filter-tab-Cancelled']`).
  4. Verify content updates to filtered bookings or empty state.
- **Expected Result:**
  - Tab buttons are clickable, active state highlights, and filtered list or legitimate empty state is shown.
- **Actual Result:** Verified.
- **Evidence Location:** TRX report.
