# SE3112 - UI / End-to-End Testing (Selenium WebDriver)

**Owner:** SE3112 UI / E2E Automation Specialist  
**Project:** EventPulse — Event Ticket Booking Platform  
**Target Solution:** `tests/e2e-selenium/EventPulse.E2E.Tests.csproj` (.NET 8.0 / C# / Selenium WebDriver 4.24.0 / NUnit 4.2.2 / Selenium Manager)

---

## 1. Purpose and Scope

The UI / End-to-End Testing suite verifies end-user workflows across the EventPulse frontend React application interacting with downstream backend microservices and API Gateway.

### Core Testing Objectives:
1. Validate user interface components, input validation, and user feedback under real browser conditions.
2. Verify routing, client-side guards, and authentication state transitions.
3. Test end-to-end integration across frontend, gateway, and core services without executing destructive payment operations.
4. Execute seamlessly in headless Google Chrome across local development and GitHub Actions CI pipelines.

---

## 2. Chosen Workflows

Based on repository reconnaissance of EventPulse React routes and backend services, three core end-to-end scenarios were implemented and hardened:

| Scenario | Workflow | Test Class | Core Verification |
|---|---|---|---|
| **Scenario 1** | **Authentication** | `LoginTests` | Input validation (empty fields, malformed email), server-side invalid credential handling, and authenticated customer session redirect. |
| **Scenario 2** | **Event Discovery** | `BrowseEventsTests` | Homepage rendering, Upcoming Events section heading, event card listings, view details links, navigation to event detail page (`/events/:id`), and dynamic support for legitimate empty states. |
| **Scenario 3** | **Customer Booking History** | `CustomerBookingHistoryTests` | Route protection redirecting unauthenticated users to `/login`, authenticated customer session rendering `/my-bookings`, page header, status filter tabs (`All`, `Confirmed`, `Pending Payment`, `Cancelled`), and non-destructive empty/booked state verification. |

---

## 3. Selenium Project Architecture

```
tests/e2e-selenium/
├── EventPulse.E2E.Tests.csproj      # .NET 8.0 test project definition
├── BaseTest.cs                      # Headless Chrome lifecycle & screenshot capture on failure
├── Constants.cs                     # Base URL & route constants with environment overrides
├── Pages/                           # Page Object Model (POM) abstractions
│   ├── LoginPage.cs                 # Login form inputs, submit, validation error getters
│   ├── BrowseEventsPage.cs          # Event cards, view details, search & empty state helpers
│   ├── EventDetailsPage.cs          # Event title, date, venue, price, back link
│   └── MyBookingsPage.cs            # Customer booking history, filter tabs, empty state
└── Tests/                           # Scenario test fixtures
    ├── LoginTests.cs                # Scenario 1 tests
    ├── BrowseEventsTests.cs         # Scenario 2 tests
    └── CustomerBookingHistoryTests.cs # Scenario 3 tests
```

---

## 4. Test Preconditions & Deterministic Test Data

| Requirement | Details |
|---|---|
| **Frontend Web App** | React + Vite dev server running on `http://localhost:5173` (or configured URL). |
| **API Gateway** | YARP Gateway listening on port `7000`. |
| **Identity Service** | Auth service listening on port `7101` with PostgreSQL connection. |
| **Event Service** | Event service listening on port `7102` with PostgreSQL connection. |
| **Booking Service** | Booking service listening on port `7103` with PostgreSQL connection. |
| **Test Customer Account** | Email: `nimal.test01@example.com` (configurable via `TEST_CUSTOMER_EMAIL`). Password: `SecurePass123!` (configurable via `TEST_CUSTOMER_PASSWORD`). |

---

## 5. Environment Variables

All sensitive values and environment-dependent URLs are passed via environment variables without hardcoded secrets:

| Variable | Description | Default Value |
|---|---|---|
| `EVENTPULSE_BASE_URL` | Base URL of the React frontend application | `http://localhost:5173` |
| `TEST_CUSTOMER_EMAIL` | Dedicated test customer user email | `nimal.test01@example.com` |
| `TEST_CUSTOMER_PASSWORD` | Dedicated test customer user password | `SecurePass123!` |
| `SELENIUM_SCREENSHOT_DIR` | Output directory for failure screenshots | `<WorkDirectory>/TestResults/screenshots` |

---

## 6. Execution Instructions

### Prerequisites
- .NET 8.0 SDK or .NET 10.0 SDK installed (`dotnet --list-sdks`)
- Google Chrome browser installed (Chromium-compatible)
- Selenium Manager resolves `chromedriver` automatically — no manual driver downloads required.

### Local Execution Command

Run the selected 3 scenarios:

```bash
dotnet test tests/e2e-selenium/EventPulse.E2E.Tests.csproj \
  --filter "FullyQualifiedName~LoginTests|FullyQualifiedName~BrowseEventsTests|FullyQualifiedName~CustomerBookingHistoryTests" \
  --logger "trx;LogFileName=selenium-results.trx" \
  --results-directory ./TestResults/selenium
```

Run a specific scenario (e.g. Authentication):

```bash
dotnet test tests/e2e-selenium/EventPulse.E2E.Tests.csproj \
  --filter "FullyQualifiedName~LoginTests"
```

---

## 7. Reliability & Synchronization Enhancements

1. **Elimination of Arbitrary Sleeps:** All arbitrary `Thread.Sleep()` calls were removed and replaced with explicit `WebDriverWait` conditions (`Displayed`, `Enabled`, URL pattern matching).
2. **Dynamic Empty-State Support:** `BrowseEventsTests` and `CustomerBookingHistoryTests` accommodate both seeded events/bookings and legitimate empty states, preventing false test failures when past events expire or fresh databases have no seed bookings.
3. **Automatic Failure Evidence:** `BaseTest.cs` monitors `TestContext.CurrentContext.Result.Outcome.Status`. Upon any test failure, a PNG screenshot is captured and saved with a sanitized, timestamped filename and attached to NUnit test context.
4. **Leak-Proof WebDriver Lifecycle:** `Driver.Quit()` and `Driver.Dispose()` execute in a structured `finally` block in `[TearDown]` to prevent background browser processes from leaking.

---

## 8. GitHub Actions Integration

The Selenium tests integrate directly into the unified team workflow `.github/workflows/se3112-testing.yml` under the dedicated job `selenium-e2e`.

### Job Workflow Specification:
```yaml
  selenium-e2e:
    name: UI / E2E Testing - Selenium WebDriver
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Restore Selenium test dependencies
        run: dotnet restore tests/e2e-selenium/EventPulse.E2E.Tests.csproj

      - name: Build Selenium test project
        run: dotnet build tests/e2e-selenium/EventPulse.E2E.Tests.csproj --configuration Release --no-restore

      - name: Run Selenium E2E Tests (Headless Chrome)
        env:
          EVENTPULSE_BASE_URL: ${{ vars.EVENTPULSE_BASE_URL || 'http://localhost:5173' }}
          TEST_CUSTOMER_EMAIL: ${{ secrets.TEST_CUSTOMER_EMAIL || 'nimal.test01@example.com' }}
          TEST_CUSTOMER_PASSWORD: ${{ secrets.TEST_CUSTOMER_PASSWORD || 'SecurePass123!' }}
          SELENIUM_SCREENSHOT_DIR: ${{ github.workspace }}/TestResults/selenium/screenshots
        run: >
          dotnet test tests/e2e-selenium/EventPulse.E2E.Tests.csproj
          --configuration Release --no-restore
          --filter "FullyQualifiedName~LoginTests|FullyQualifiedName~BrowseEventsTests|FullyQualifiedName~CustomerBookingHistoryTests"
          --logger "trx;LogFileName=selenium-results.trx"
          --results-directory ./TestResults/selenium

      - name: Upload Selenium TRX Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: selenium-test-results
          path: TestResults/selenium/*.trx

      - name: Upload Selenium Failure Screenshots
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: selenium-failure-screenshots
          path: TestResults/selenium/screenshots/*.png
          if-no-files-found: ignore
```

---

## 9. Failure Troubleshooting & Known Limitations

- **Connection Refused (`ERR_CONNECTION_REFUSED`):** The React frontend (`http://localhost:5173`) or API Gateway (`http://localhost:7000`) is offline. Ensure `npm run dev` and `backend/dev-start.ps1` are running prior to executing tests.
- **Chrome Version Mismatch:** Handled automatically by Selenium Manager. If running in locked offline environments, ensure Google Chrome is updated to a stable release.
- **No Active Events:** EventPulse hides past events by business rule. If `BrowseEvents` shows the empty state, this is verified as valid behavior by the updated test suite.
