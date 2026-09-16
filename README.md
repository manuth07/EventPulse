# EventPulse

EventPulse is a distributed, microservice-based event discovery and ticketing platform. The system is designed with a domain-driven microservices architecture, utilizing an API Gateway as a single ingress point, decoupled service boundaries with dedicated persistence schemas, asynchronous communication capabilities, and a responsive single-page application frontend.

---

## Architecture Overview

```
                      +-----------------------------+
                      |       React Frontend        |
                      |    (http://localhost:5173)  |
                      +--------------+--------------+
                                     |
                                     v
                      +-----------------------------+
                      |   YARP API Gateway (:7000)   |
                      +--------------+--------------+
                                     |
         +-------------------+-------+-------+-------------------+
         |                   |               |                   |
         v                   v               v                   v
+-----------------+ +-----------------+ +-----------------+ +-----------------+
| Identity Service| |  Event Service  | | Booking Service | | Payment Service |
|     (:7101)     | |     (:7102)     | |     (:7103)     | |     (:7104)     |
+--------+--------+ +--------+--------+ +--------+--------+ +--------+--------+
         |                   |                   |                   |
         v                   v                   v                   v
+-----------------+ +-----------------+ +-----------------+ +-----------------+
|   PostgreSQL    | |   PostgreSQL    | |   PostgreSQL    | |   PostgreSQL    |
| (eventpulse_id) | | (eventpulse_ev) | | (eventpulse_bk) | | (eventpulse_pm) |
+-----------------+ +-----------------+ +-----------------+ +-----------------+
```

### Services & Ingress Points

| Component | Port | Technology | Primary Responsibilities |
| :--- | :--- | :--- | :--- |
| **Frontend** | `5173` | React, Vite, Tailwind/CSS | Client application, authentication flows, event discovery, booking UI |
| **API Gateway** | `7000` | ASP.NET Core, YARP | Single reverse proxy ingress, request routing, CORS policy management |
| **Identity Service** | `7101` | ASP.NET Core (.NET 10), EF Core | Authentication, user registration, JWT issuance, RBAC, organizer onboarding |
| **Event Service** | `7102` | ASP.NET Core (.NET 10), EF Core | Event catalog, lifecycle state machine, ticket tier definitions, cover storage |
| **Booking Service** | `7103` | ASP.NET Core (.NET 10), EF Core | Cart management, ticket reservations, inventory verification |
| **Payment Service** | `7104` | ASP.NET Core (.NET 10), EF Core | Stripe checkout processing, transaction logging, payment confirmations |
| **Database** | `5433` | PostgreSQL 16 (Docker) | Isolated schemas for each microservice boundary |
| **Azurite** | `10000` | Azure Storage Emulator | Local Blob storage for event posters and media |

---

## Prerequisites

Ensure the following runtimes and tools are installed on your workstation:

- **.NET 10 SDK** (v10.0 or later)
- **Node.js** (v18 or v20 LTS) and **npm**
- **Docker Desktop** (with Docker Compose support)
- **PowerShell** (Windows) or **Bash** (macOS/Linux)
- **Git**

---

## Environment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/manuth07/EventPusle.git
cd EventPulse
```

### 2. Configure Environment Files

Create the root `.env` file from the provided template:

```bash
cp .env.example .env
```

Create the frontend configuration file:

```bash
cp frontend/.env.example frontend/.env.local
```

### 3. Start Infrastructure Services

Launch the containerized PostgreSQL instance and Azurite blob emulator:

```bash
docker compose up -d
```

Verify that the containers are healthy and running:

```bash
docker compose ps
```

### 4. Configure Development Secrets (.NET User Secrets)

Services authenticate inter-service requests using shared JWT tokens. Configure the development signing key across backend services:

```powershell
dotnet user-secrets set "Jwt:Key" "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK" --project backend/services/identity-service/src/EventPulse.IdentityService
dotnet user-secrets set "Jwt:Key" "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK" --project backend/services/event-service/src/EventPulse.EventService
dotnet user-secrets set "Jwt:Key" "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK" --project backend/services/booking-service/src/EventPulse.BookingService
```

*(Optional)* Configure development organizer credentials in Identity Service:

```powershell
dotnet user-secrets set "DevOrganizer:Enabled" "true" --project backend/services/identity-service/src/EventPulse.IdentityService
dotnet user-secrets set "DevOrganizer:Email" "organizer@eventpulse.dev" --project backend/services/identity-service/src/EventPulse.IdentityService
dotnet user-secrets set "DevOrganizer:Password" "Organizer123!" --project backend/services/identity-service/src/EventPulse.IdentityService
```

---

## Running the Application

### Option 1: Automated Backend Startup (Recommended)

Run the PowerShell runner to build and launch all backend services concurrently:

```powershell
.\backend\dev-start.ps1
```

To stop all backend processes, press `Ctrl + C`.

### Option 2: Manual Service Startup

Alternatively, run each service in a separate terminal:

```bash
# 1. API Gateway
dotnet run --project backend/gateway/src/EventPulse.Gateway

# 2. Identity Service
dotnet run --project backend/services/identity-service/src/EventPulse.IdentityService

# 3. Event Service
dotnet run --project backend/services/event-service/src/EventPulse.EventService

# 4. Booking Service
dotnet run --project backend/services/booking-service/src/EventPulse.BookingService
```

### Starting the Frontend

In a separate terminal window:

```bash
cd frontend
npm install
npm run dev
```

The frontend application will be accessible at: `http://localhost:5173`

---

## Health Checks & API Verification

Each service exposes a health probe consumed by the gateway and external monitoring:

- Gateway Health: `http://localhost:7000/health`
- Identity Service: `http://localhost:7101/health`
- Event Service: `http://localhost:7102/health`
- Booking Service: `http://localhost:7103/health`

In development mode, OpenAPI specifications can be inspected at:
- Gateway: `http://localhost:7000/openapi/v1.json`
- Identity Service: `http://localhost:7101/openapi/v1.json`
- Event Service: `http://localhost:7102/openapi/v1.json`

---

## Database Migrations

Database schemas are managed using Entity Framework Core Code-First migrations. In development mode, migrations and default roles are applied automatically upon service startup.

To apply migrations manually:

```bash
# Identity Service
dotnet ef database update --project backend/services/identity-service/src/EventPulse.IdentityService

# Event Service
dotnet ef database update --project backend/services/event-service/src/EventPulse.EventService

# Booking Service
dotnet ef database update --project backend/services/booking-service/src/EventPulse.BookingService
```

---

## Running Tests & Code Quality

Execute automated test suites across all backend projects:

```bash
# Run all unit and integration tests
dotnet test

# Run tests for a specific service
dotnet test backend/services/identity-service/tests/EventPulse.IdentityService.Tests
dotnet test backend/services/event-service/tests/EventPulse.EventService.Tests

# Run frontend linting
cd frontend
npm run lint
```

---

## Project Structure

```
EventPulse/
├── .env.example                               # Root environment template
├── docker-compose.yml                         # Infrastructure definitions (PostgreSQL, Azurite)
├── DEVELOPMENT_GUIDELINES.md                  # Development guidelines & architectural standards
├── backend/
│   ├── dev-start.ps1                          # Multi-service development launcher
│   ├── gateway/                               # YARP API Gateway
│   └── services/
│       ├── identity-service/                  # Authentication, accounts, roles & onboarding
│       ├── event-service/                     # Event catalog, approval workflow & ticket tiers
│       ├── booking-service/                   # Cart, temporary reservations & booking records
│       └── payment-service/                   # Stripe payment processing & audit logs
├── frontend/                                  # React (Vite) single-page application
├── infrastructure/                            # Docker initialization assets
├── docs/                                      # Architectural specifications & QA documentation
└── tests/                                     # Integration & performance test suites
```