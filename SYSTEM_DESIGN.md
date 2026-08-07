# System Design Document — Unified Service Scheduler
**Automotive Retail | v1 MVP**

---

## 1. Executive Summary & Business Context

| | |
|---|---|
| **Project** | Enterprise-grade service scheduling and throughput optimisation engine for automotive dealerships |
| **Core Problem** | Eliminates manual calendar bottlenecks, prevents double-booking of physical resources (Service Bays + Technicians), and mitigates cascading-failure caused by job overruns |
| **Key Metrics** | Bay utilisation rate · Technician billable hours · Schedule conflict count · SLA adherence |

---

## 2. Scope & Assumptions

### In-Scope (v1 MVP)
- Resource-constrained booking across Customer, Vehicle/VIN, Dealership, and multi-Service Lines
- Real-time availability validation with multi-resource lock checks (Bay + Technician)
- Mandatory 10-minute automated buffer window post-appointment
- Appointment lifecycle tracking: `Pending → Confirmed → InProgress → Completed | Cancelled`
- Immutable audit logging for every state transition

### Explicit Non-Goals (v1)
- Intentional overbooking of express lanes
- Direct payroll / time-clock integrations
- Automated SMS/Email notification microservices (domain events emitted only)

---

## 3. High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  ASP.NET Core Web API                   │
│  AppointmentsController  ·  AuthController               │
│  SeedController  ·  FluentValidation  ·  JWT Bearer Auth  │
├─────────────────────────────────────────────────────────┤
│              Service Layer (Interfaces)                  │
│  ISchedulingService  ·  IUserCredentialStore             │
├─────────────────────────────────────────────────────────┤
│              Core Scheduling Engine                      │
│  SchedulingService — availability · buffer · collision    │
│  AppointmentStateMachine — transition table (OCP)        │
│  IOptions<SchedulingOptions> — configurable buffer       │
├─────────────────────────────────────────────────────────┤
│         Entity Framework Core (Data Access Layer)        │
│  SchedulerDbContext — indexed time-window queries         │
├─────────────────────────────────────────────────────────┤
│        SQLite (dev/test) │ PostgreSQL / SQL Server       │
└─────────────────────────────────────────────────────────┘
```

**Architectural style:** Modular Monolith / Service-Oriented Web API, with clean layer boundaries designed for future DMS (Dealer Management System) decoupling.

### Component Responsibilities

| Layer | Responsibility |
|---|---|
| **API Layer** | HTTP routing, request/response shaping, 409 conflict propagation, JWT Bearer authentication |
| **Service Interfaces** | `ISchedulingService`, `IUserCredentialStore` — decoupling contracts for DI and testing |
| **SchedulingService** | Availability algorithm, buffer enforcement, state-machine transition guard, ACID transaction orchestration |
| **SchedulerDbContext** | EF Core entity mapping, composite index definitions, DbSet exposure |
| **Domain Models** | Pure data entities — no business logic |

---

## 4. Domain Model & Entity Relationships

```
Customer ──< Vehicle
    │
    └──< Appointment >── ServiceBay
              │      └── Technician
              │
              └──< AppointmentServiceLine >── ServiceType
              └──< AppointmentAuditLog
```

### Entities

| Entity | Key Fields |
|---|---|
| `Customer` | Id, Name, Email, Phone |
| `Vehicle` | Id, **Vin** (unique index), Make, Model, Year, CustomerId |
| `ServiceBay` | Id, Name, DealershipLocation, **CapabilityTag** (General/HeavyRepair/EvCertified), IsActive |
| `Technician` | Id, Name, DealershipLocation, **Skill** (tiered enum), ShiftStart, ShiftEnd, IsActive |
| `ServiceType` | Id, Name, DefaultDurationMinutes, RequiredSkill, RequiredBayCapability |
| `Appointment` | Id, CustomerId, VehicleId, DealershipLocation, **ServiceBayId**, **TechnicianId**, StartTime, **EndTime** (includes buffer), Status, CreatedAt, UpdatedAt |
| `AppointmentServiceLine` | Id, AppointmentId, ServiceTypeId, DurationMinutes, RequiredSkill, RequiredBayCapability |
| `AppointmentAuditLog` | Id, AppointmentId, FromStatus, ToStatus, ChangedBy, Reason, ChangedAt |

### Capability & Skill Tiers

Both `BayCapabilityTag` and `TechnicianSkill` use ascending integer enum values so a single `>=` comparison expresses "capable-or-better":

```
General (0)  <  HeavyRepair (1)  <  EvCertified (2)
```

---

## 5. Core Workflows & Algorithmic Design

### 5.1 Multi-Resource Availability Algorithm

```
BookAppointmentAsync(request):
  1. Load ServiceTypes for all requested IDs
  2. totalDuration = Σ(ServiceType.DefaultDurationMinutes) + 10 min buffer
     endTime = request.StartTime + totalDuration
  3. minBayCapability  = MAX(serviceTypes.RequiredBayCapability)
     minTechSkill      = MAX(serviceTypes.RequiredSkill)
  4. busyBayIds  = Appointments WHERE location=X
                                AND status NOT IN (Cancelled, Completed)
                                AND StartTime < endTime
                                AND EndTime   > startTime   ← overlap condition
                                SELECT ServiceBayId
  5. availableBay = ServiceBays WHERE location=X
                                 AND IsActive=true
                                 AND CapabilityTag >= minBayCapability
                                 AND Id NOT IN busyBayIds
  6. (same pattern for Technician)
  7. Persist Appointment + ServiceLines + AuditLog in one ACID transaction
```

### 5.2 Overlap Collision Condition

$$\text{Overlap} \iff (\text{ExistingStart} < \text{RequestedEnd}) \;\land\; (\text{ExistingEnd} > \text{RequestedStart})$$

This is the standard interval-overlap predicate. It correctly handles:
- Exact matches
- Partial overlaps from either side
- One appointment wholly contained inside another

### 5.3 Cancellation & Resource Release

When an appointment is cancelled its status becomes `Cancelled`. All availability queries explicitly exclude `Cancelled` (and `Completed`) appointments, so resources are freed **instantly** with no additional cleanup step.

### 5.4 Mandatory 10-Minute Recovery Buffer

`EndTime` stored on every appointment already incorporates the buffer:

```
EndTime = StartTime + Σ(ServiceLine.DurationMinutes) + 10
```

Because collision checks use `EndTime`, the buffer is automatically enforced for every subsequent booking attempt against the same resource.

---

## 6. Database Index Strategy

| Index | Purpose |
|---|---|
| `Vehicle.Vin` (unique) | Fast VIN lookups and duplicate prevention |
| `Appointment(ServiceBayId, StartTime, EndTime)` | Time-window collision scan per bay |
| `Appointment(TechnicianId, StartTime, EndTime)` | Time-window collision scan per technician |
| `Appointment(DealershipLocation, StartTime)` | Location-scoped schedule views |

---

## 7. Appointment Lifecycle

```
[Created] ──► Pending
                │
                ▼
            Confirmed ──► InProgress ──► Completed
                │               │
                └───────────────┴──► Cancelled
```

Every transition is recorded in `AppointmentAuditLog` with actor ID, timestamp, and reason — providing a full dispute-resolution trail.

---

## 8. Security

| Concern | Implementation |
|---|---|
| **Authentication** | JWT Bearer — all endpoints except `POST /api/auth/token` require a valid token |
| **Authorization** | Role-based (`[Authorize(Roles = "...")]`) — `ServiceAdvisor` books/cancels; `Admin` seeds; both can read |
| **Credential storage** | `DemoUserStore` hashes passwords with ASP.NET Identity `PasswordHasher<T>` (PBKDF2) |
| **Token claims** | `sub`, `jti` (unique per token), `name`, `role`; validated issuer, audience, lifetime, and signing key |
| **Swappability** | `IUserCredentialStore` interface — replace `DemoUserStore` with enterprise SSO by registering a different implementation; zero other code changes |
| **Key management** | `Jwt:Key` is absent from `appsettings.json`; must be supplied via User Secrets, environment variable, or a secrets vault |

---

## 9. SOLID Principles & Design Patterns

### SOLID

| Principle | Applied |
|---|---|
| **SRP** | Controller handles HTTP only; `SchedulingService` owns domain logic; `BookAppointmentRequestValidator` owns input rules |
| **OCP** | `AppointmentStateMachine` encodes all valid transitions in a data table — adding a new state requires one dictionary entry, no method changes |
| **LSP** | `ISchedulingService` and `IUserCredentialStore` have clean contracts; any conforming implementation is substitutable |
| **ISP** | Interfaces are cohesive and minimal; consumers depend only on what they use |
| **DIP** | Controllers and `ServiceCollectionExtensions` depend on interfaces, not concrete classes; `IOptions<SchedulingOptions>` decouples configuration |

### Design Patterns

| Pattern | Where |
|---|---|
| **Strategy** | `ISchedulingService` / `IUserCredentialStore` — swappable implementations via DI |
| **State Machine** | `AppointmentStateMachine` — transition table drives all status change validation |
| **Options Pattern** | `IOptions<SchedulingOptions>` — `BufferMinutes` configurable per environment without code changes |
| **Static Factory** | `AppointmentResponse.From(appointment)` — controlled mapping from domain entity to response DTO |
| **Validator** | FluentValidation `AbstractValidator<BookAppointmentRequest>` — validation rules isolated from controller |

---

## 10. API Surface

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/token` | None | Issue a JWT for the given username + password |
| `POST` | `/api/appointments` | `ServiceAdvisor` | Book a new appointment |
| `GET` | `/api/appointments/{id}` | `ServiceAdvisor`, `Admin` | Retrieve appointment with service lines and audit log |
| `POST` | `/api/appointments/{id}/cancel` | `ServiceAdvisor` | Cancel and release resources |
| `POST` | `/api/seed` | `Admin` | Populate database with sample data (dev only) |

---

## 11. Non-Functional Requirements & Resilience

| Concern | Approach |
|---|---|
| **Performance** | `< 500 ms` availability lookups via composite indexed foreign keys and lean LINQ projections |
| **Data Integrity** | ACID transactional boundary (EF Core `BeginTransactionAsync`) wraps appointment creation, service lines, and audit log — all commit or all rollback |
| **Auditability** | Immutable `AppointmentAuditLog` table; every state change captured with actor and reason |
| **Concurrency** | Optimistic collision detection via time-overlap queries before write; designed for future soft-hold (Redis) layer in high-concurrency showroom scenarios |
| **Testability** | EF Core InMemory provider enables fast, isolated unit tests with no external dependencies |

---

## 12. Future Roadmap (Post-MVP)

- Soft-hold reservation system (Redis 5-minute slot lock) for high-concurrency showroom
- PostgreSQL / SQL Server migration with EF Core migrations
- Domain event emission for SMS/Email notification microservices
- Express-lane intentional overbooking mode
- Technician shift-window enforcement in availability algorithm
- Repository pattern abstraction over `SchedulerDbContext` for full DIP compliance
