# ServiceScheduler — Unified Service Scheduler API

Enterprise-grade service scheduling engine for automotive dealerships. Prevents double-booking of Service Bays and Technicians, enforces mandatory recovery buffers, and tracks full appointment lifecycle with an immutable audit trail.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

---

## Build & Run

```bash
# Clone / navigate to the solution root
cd D:\ServiceScheduler

# Restore and build
dotnet build

# Run the API (starts on http://localhost:5265)
dotnet run --project ServiceScheduler.Api
```

---

## Run Tests

```bash
dotnet test ServiceScheduler.Tests
```

Three xUnit tests exercise the core engine directly against an isolated InMemory database:

| Test | Validates |
|---|---|
| `Book_ValidRequest_ReturnsConfirmedAppointment` | Happy-path booking returns `Confirmed` status |
| `Book_BayAndTechAlreadyBooked_ReturnsConflictError` | Overlap collision detection rejects double-booking |
| `Cancel_ReleasesResources_AllowsSubsequentBookingAtSameSlot` | Cancellation immediately frees resources |

---

## Quickstart — End-to-End Demo

All requests below use `http://localhost:5265`.

### 0. Get a JWT token

All appointment endpoints require a Bearer token. Use the dev token endpoint to generate one:

```json
POST /api/auth/token
{
  "username": "advisor",
  "password": "Advisor123!"
}
```

Built-in demo accounts:

| Username | Password | Role |
|---|---|---|
| `advisor` | `Advisor123!` | `ServiceAdvisor` |
| `admin` | `Admin123!` | `Admin` |
| `customer` | `Customer123!` | `Customer` |

Response:
```json
{
  "token": "eyJhbGci...",
  "role": "ServiceAdvisor"
}
```

**In Swagger UI:** click the **Authorize 🔒** button at the top, paste the token value (without any prefix), then click **Authorize**.

**In curl / HTTP clients:** add the header:
```
Authorization: Bearer <token>
```

> The token is valid for 60 minutes. Re-call this endpoint if it expires.

---

### 1. Seed sample data
```
POST /api/seed
```
Creates 3 service types, 3 bays, 3 technicians, 2 customers, 2 vehicles.

### 2. Book an appointment
```json
POST /api/appointments
{
  "customerId": 1,
  "vehicleId": 1,
  "dealershipLocation": "Main",
  "serviceTypeIds": [1],
  "startTime": "2026-08-05T09:00:00Z",
  "advisorId": "advisor1"
}
```
Returns `201 Created` with the appointment. `EndTime` automatically includes the 10-minute recovery buffer.

### 3. Attempt a conflicting booking (expect 409)
```json
POST /api/appointments
{
  "customerId": 2,
  "vehicleId": 2,
  "dealershipLocation": "Main",
  "serviceTypeIds": [1],
  "startTime": "2026-08-05T09:00:00Z",
  "advisorId": "advisor2"
}
```

### 4. View the appointment with audit trail
```
GET /api/appointments/1
```

### 5. Cancel the appointment
```json
POST /api/appointments/1/cancel
{
  "cancelledBy": "advisor1",
  "reason": "Customer no-show"
}
```

### 6. Rebook the freed slot (now succeeds)
Repeat the Step 3 payload — resources have been released.

---

### Full lifecycle: Start → Complete

Book a fresh appointment, then walk it through all active states:

### 7. Start the appointment (Confirmed → InProgress)
```json
POST /api/appointments/{id}/start
{
  "changedBy": "advisor",
  "reason": "Vehicle checked in"
}
```

### 8. Complete the appointment (InProgress → Completed)
```json
POST /api/appointments/{id}/complete
{
  "changedBy": "advisor",
  "reason": "Work completed successfully"
}
```

### 9. Attempt to cancel a completed appointment (expect 409)
```json
POST /api/appointments/{id}/cancel
{
  "cancelledBy": "advisor",
  "reason": "Should fail — Completed is a terminal state"
}
```
`AppointmentStateMachine` rejects any transition out of `Completed` or `Cancelled`.

---

## Project Structure

```
ServiceScheduler/
├── ServiceScheduler.sln
├── SYSTEM_DESIGN.md
├── README.md
├── ServiceScheduler.Api/
│   ├── Controllers/
│   │   ├── AppointmentsController.cs   — booking, retrieval, start, complete, cancellation
│   │   ├── AuthController.cs           — JWT token issuance (POST /api/auth/token)
│   │   └── SeedController.cs           — dev-only data seeding
│   ├── Data/
│   │   └── SchedulerDbContext.cs       — EF Core context + index configuration
│   ├── Infrastructure/
│   │   └── ServiceCollectionExtensions.cs  — grouped DI registration extension methods
│   ├── Models/                         — domain entities, request/response DTOs
│   │   └── AppointmentStateMachine.cs  — state transition table (OCP)
│   ├── Options/
│   │   └── SchedulingOptions.cs        — configurable scheduling settings (e.g. BufferMinutes)
│   ├── Services/
│   │   ├── Interface/
│   │   │   ├── ISchedulingService.cs
│   │   │   └── IUserCredentialStore.cs
│   │   ├── DemoUserStore.cs            — PBKDF2-hashed in-memory credential store
│   │   └── SchedulingService.cs        — availability engine, buffer, ACID transaction
│   └── Program.cs
└── ServiceScheduler.Tests/
    └── SchedulingServiceTests.cs
```

---

## AI Collaboration Narrative

This project was built in a pair-programming session with **GitHub Copilot (Claude Sonnet 4.5)** as the AI agent. The following describes how the collaboration was structured, where AI added velocity, and where human judgment was essential.

### What the AI was directed to do

| Task | AI Contribution |
|---|---|
| Solution scaffolding | Generated the `dotnet new sln / webapi / xunit` command sequence and folder structure |
| Domain model design | Drafted all entity classes from the system design spec, including navigation properties and nullable reference annotations |
| EF Core context | Proposed the composite index strategy for time-window collision queries (`ServiceBayId, StartTime, EndTime`) |
| Availability algorithm | Implemented the interval-overlap predicate (`startA < endB && endA > startB`) and the tiered capability/skill `>=` filter |
| ACID transaction wrapper | Scaffolded the `BeginTransactionAsync / Commit / Rollback` pattern around the multi-table booking write |
| xUnit tests | Generated the three core test cases covering success, collision rejection, and cancellation release |
| NuGet package selection | Suggested EF Core 8.0.8, FluentValidation, and JwtBearer versions aligned with .NET 8 |

### Where human judgment was applied

- **Architectural decisions:** The choice of Modular Monolith over microservices, and InMemory DB for the MVP, was a deliberate human decision — AI presented options but did not choose.
- **Enum ordering strategy:** The decision to use ascending integer enum values for `BayCapabilityTag` and `TechnicianSkill` (enabling `>=` comparisons) was a human refinement of the AI's initial draft which used separate tag lists.
- **Bug triage:** The AI initially used `Microsoft.EntityFrameworkCore.InMemory.Diagnostics` as the namespace for `InMemoryEventId`. Human review caught the compile error; the correct namespace `Microsoft.EntityFrameworkCore.Diagnostics` was identified and applied.
- **Verification discipline:** Every AI-generated component was verified by running `dotnet build` and `dotnet test` before acceptance. No code was merged without a green test run.
- **Scope control:** AI suggested additional features (shift-window enforcement, FluentValidation rules, JWT middleware wiring). These were deliberately deferred to post-MVP to keep v1 focused.

### Lessons learned

- AI dramatically accelerates boilerplate (entity classes, DbContext, controller scaffolding) — tasks that would take 30–60 minutes were done in seconds.
- The AI's output required architectural review, not just syntax review. The most valuable human interventions were at the design level (index strategy, enum ordering, transaction placement), not line-by-line corrections.
- A clear system design document given upfront significantly improved AI output quality — vague prompts produced vague code.
- Running tests immediately after each AI-generated component created a tight feedback loop that caught issues before they compounded.
