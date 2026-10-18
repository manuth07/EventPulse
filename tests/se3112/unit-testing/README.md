# SE3112 - Unit Testing & Mocking (xUnit + Moq)

**Owner:** T.M.I.O. Thilakarathne (IT24102608)
**Project:** EventPulse (SE3022 base code) - Booking Service and Payment Service

## Purpose

Verify EventPulse business logic in isolation, without a database server, Kafka, Azure, Stripe or real payments.

## Components tested

| Service | Component | Rules covered |
|---|---|---|
| Booking | `CancellationEligibilityChecker` | 48-hour cutoff boundary, booking-status rules |
| Booking | `BookingCancellationService` | inventory restore/release, refund-event logic |
| Payment | `PaymentRefundService` | refund amount cap, non-refundable statuses, zero/negative amounts, Stripe outcome mapping |

## Techniques

| Technique | Where it is used |
|---|---|
| Parameterized tests (`[Theory]` + `[InlineData]`) | All 8 test methods listed below |
| Valid, invalid and boundary values | 48h + 1s (eligible) vs exactly 48h (rejected); refund 0 and -10; request above payment amount; undefined status 99 |
| Stub | `FixedTimeProvider` (frozen clock); `ICancellationEligibilityChecker` and `IStripeRefundService` return canned results via Moq `.Setup()` |
| Mock + `.Verify()` | `IEventAvailabilityClient`, `IBookingEventPublisher`, `IStripeRefundService`, `IPaymentEventPublisher`, checked with `Times.Once()`, `Times.Never()` and `VerifyNoOtherCalls()` |
| Fake | EF Core InMemory database (unique per test) |

## Test files

- `backend/services/booking-service/tests/EventPulse.BookingService.Tests/Services/CancellationPolicyParameterizedTests.cs` (13 cases)
- `backend/services/payment-service/tests/EventPulse.PaymentService.Tests/PaymentRefundParameterizedTests.cs` (11 cases)

Both classes are tagged `[Trait("Category", "SE3112-Unit")]`, which is the filter used locally and in CI.

## Test cases (24)

### Booking: cancellation policy (13)

| # | Test method | Input | Expected |
|---|---|---|---|
| 1 | Cutoff boundary | 7 days before event | eligible |
| 2 | Cutoff boundary | 48h + 1s before | eligible |
| 3 | Cutoff boundary | exactly 48h before | `CANCELLATION_WINDOW_CLOSED` |
| 4 | Cutoff boundary | 1h before | `CANCELLATION_WINDOW_CLOSED` |
| 5 | Cutoff boundary | 0s (event starting) | `EVENT_ALREADY_STARTED` |
| 6 | Status rules | `Cancelled` | `ALREADY_CANCELLED` |
| 7 | Status rules | `PaymentFailed` | `PAYMENT_NOT_ELIGIBLE` |
| 8 | Status rules | undefined status (99) | `INVALID_BOOKING_STATUS` |
| 9 | Service collaborators | `Confirmed`, amount 100 | restore stock once, refund event once |
| 10 | Service collaborators | `Confirmed`, amount 0 | restore stock once, no refund |
| 11 | Service collaborators | `PendingPayment`, amount 100 | release hold once, no refund |
| 12 | Rejected cancellation | policy says `ALREADY_CANCELLED` | booking unchanged, no calls |
| 13 | Rejected cancellation | policy says `CANCELLATION_WINDOW_CLOSED` | booking unchanged, no calls |

### Payment: refund processing (11)

| # | Test method | Input (payment = 150) | Expected |
|---|---|---|---|
| 14 | Amount cap | refund 50, nothing refunded before | Stripe gets 50, payment `PartiallyRefunded` |
| 15 | Amount cap | refund 500 | capped to 150, payment `Refunded` |
| 16 | Amount cap | refund 80, 100 already refunded | capped to 50, payment `Refunded` |
| 17 | Non-refundable status | payment `Pending` | `NotRefundable`, amount 0, Stripe never called |
| 18 | Non-refundable status | payment `Failed` | `NotRefundable`, Stripe never called |
| 19 | Non-refundable status | payment `RefundFailed` | `NotRefundable`, Stripe never called |
| 20 | Zero/negative amount | refund 0 | `NotRefundable`, payment unchanged, Stripe never called |
| 21 | Zero/negative amount | refund -10 | `NotRefundable`, payment unchanged, Stripe never called |
| 22 | Stripe outcome | `succeeded` | refund `Completed`, payment `Refunded`, event published once |
| 23 | Stripe outcome | `pending` | refund `Pending`, payment stays `Succeeded`, no event |
| 24 | Stripe outcome | failure | refund `Failed`, payment `RefundFailed`, no event |

### Existing tests reused

`BookingCancellationServiceTests` and `CancellationEligibilityCheckerTests` (Booking) and `PaymentRefundServiceTests` (Payment) already contain Moq mocks and `.Verify()` calls. Baseline runs before adding the new files:

- Booking, `--filter "FullyQualifiedName~Cancellation"`: 32 passed, 0 failed
- Payment, `--filter "FullyQualifiedName~Refund"`: 13 passed, 0 failed

None of these existing files had a `[Theory]`; the new files add the parameterized coverage.

## Run locally

```bash
# Booking service (13 tests)
dotnet test backend/services/booking-service/tests/EventPulse.BookingService.Tests/EventPulse.BookingService.Tests.csproj --filter "Category=SE3112-Unit"

# Payment service (11 tests)
dotnet test backend/services/payment-service/tests/EventPulse.PaymentService.Tests/EventPulse.PaymentService.Tests.csproj --filter "Category=SE3112-Unit"
```

## Results

GitHub Actions, workflow "SE3112 Microservice Testing Pipeline", job "Unit Testing & Mocking - xUnit + Moq":

```
Booking: Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 1 s - EventPulse.BookingService.Tests.dll (net10.0)
Payment: Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 1 s - EventPulse.PaymentService.Tests.dll (net10.0)
```

Local run of the Payment tests: `Total tests: 11, Passed: 11, Failed: 0`.

The TRX result files (`unit-tests.trx`, `payment-unit-tests.trx`) are uploaded as the `unit-test-results` artifact.

## How mocks isolate dependencies

- The Event service REST client, the Kafka publishers and the Stripe refund service are Moq mocks, so no network call, message or payment is ever made.
- The eligibility policy is stubbed in the service tests, so only the service's own logic is under test.
- The clock is a frozen stub, so the 48-hour boundary tests are deterministic.
- `Verify(..., Times.Never())` and `VerifyNoOtherCalls()` prove that rejected requests touch no collaborator.

## CI

Job `unit-testing` in `.github/workflows/se3112-testing.yml` runs one `dotnet test` step per service on a GitHub-hosted runner and uploads the TRX files. A failed assertion returns a nonzero exit code, which fails the job.

```bash
dotnet test <test-project>.csproj \
  --configuration Release --no-restore \
  --filter "Category=SE3112-Unit" \
  --logger "trx;LogFileName=<name>.trx" \
  --results-directory ./TestResults/unit
```
