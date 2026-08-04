# Video Presentation Script — Unified Service Scheduler
**Target duration: 5–8 minutes**

---

## Segment 1 — Introduction & Background (0:00 – 1:00)

> "Hi, I'm [name]. The scenario I chose is an automotive dealership service department.
> The core problem is a real one: service advisors manage bay and technician schedules
> manually, which leads to double-bookings, schedule cascades when a job overruns, and
> no audit trail when disputes arise.
>
> I built the Unified Service Scheduler — a .NET 8 Web API that enforces resource
> constraints in real time, so two advisors can never book the same bay or technician
> into a conflicting time window."

**Key point to land:** This is a concurrency and resource-contention problem, not just a CRUD app.

---

## Segment 2 — Architecture Overview (1:00 – 2:30)

> "The solution is a Modular Monolith — one deployable unit with clean internal layers."

Walk through the stack:

1. **ASP.NET Core Web API** — three endpoints: book, view, cancel
2. **SchedulingService** — the availability algorithm lives here, isolated from HTTP concerns
3. **EF Core + InMemory DB** — production-ready schema, InMemory for the demo
4. **Domain models** — show the entity relationship briefly: Customer → Vehicle → Appointment → ServiceLines + AuditLog

> "One design decision worth calling out: `BayCapabilityTag` and `TechnicianSkill` are
> integer enums ordered by tier — General=0, HeavyRepair=1, EvCertified=2. This lets
> a single greater-than-or-equal comparison match 'sufficient-or-better' resources,
> rather than maintaining separate tag lists."

---

## Segment 3 — Live Demo (2:30 – 5:30)

Use a REST client (Postman, Bruno, or curl). Walk through each step while narrating.

### Step 1 — Seed (30 sec)
```
POST /api/seed
```
> "This populates three bays, three technicians across General, HeavyRepair, and
> EvCertified tiers, plus two customers and two vehicles."

### Step 2 — Book appointment (60 sec)
```json
POST /api/appointments
{ "customerId": 1, "vehicleId": 1, "dealershipLocation": "Main",
  "serviceTypeIds": [1], "startTime": "2026-08-05T09:00:00Z", "advisorId": "advisor1" }
```
> "Notice the response: EndTime is 09:40, not 09:30. The engine automatically adds
> the mandatory 10-minute recovery buffer on top of the 30-minute Oil Change duration.
> This is what prevents schedule cascades."

### Step 3 — Collision rejection (45 sec)
Repeat the same request for the same slot.
> "409 Conflict. The overlap predicate — ExistingStart less than RequestedEnd AND
> ExistingEnd greater than RequestedStart — fires and the second booking is rejected
> before touching the database write path."

### Step 4 — View with audit trail (30 sec)
```
GET /api/appointments/1
```
> "Every booking and state change is logged to the AppointmentAuditLog — who made the
> change, when, and why. This is the dispute-resolution trail."

### Step 5 — Cancel and rebook (45 sec)
```json
POST /api/appointments/1/cancel
{ "cancelledBy": "advisor1", "reason": "Customer no-show" }
```
Then rebook the same slot.
> "Resources are freed the instant the status flips to Cancelled — no background job,
> no cleanup step. The collision query simply excludes Cancelled appointments."

---

## Segment 4 — AI Collaboration Summary (5:30 – 7:00)

> "I used GitHub Copilot throughout the build. Here's an honest breakdown."

**What AI handled well:**
- Scaffolding entity classes and the EF Core context from the design spec — saved ~45 minutes
- Generating the xUnit test skeletons once I described the three scenarios
- Proposing the composite index strategy for time-window queries

**Where I had to intervene:**
- The AI initially put `InMemoryEventId` in the wrong namespace — a compile error I caught by running `dotnet build` immediately
- AI suggested adding FluentValidation rules, JWT wiring, and shift-window enforcement during v1. I scoped those out to keep the MVP focused
- The tiered enum ordering (`General=0, HeavyRepair=1`) was my refinement — AI's first draft used string tag comparisons

> "The key lesson: AI accelerates implementation, but it doesn't own architecture.
> Giving it a detailed design document upfront produced significantly better output
> than open-ended prompts. And running tests after every generated component created
> a feedback loop that caught issues before they compounded."

---

## Segment 5 — Closing (7:00 – 8:00)

> "To summarise: the scheduler enforces hard resource constraints via interval-overlap
> collision detection, a mandatory 10-minute buffer baked into every EndTime, and ACID
> transactions on the booking write path. The design is intentionally simple for v1,
> with clear extension points — a Redis soft-hold layer, PostgreSQL migration, and
> domain event emission for notifications are all documented in the system design as
> post-MVP work.
>
> Thank you."

---

## Presenter Notes

- Keep the terminal and REST client side-by-side so the audience can see request and response simultaneously
- If time is tight, skip Step 4 (audit trail view) — Steps 2, 3, and 5 are the critical demonstrations
- Have the app already running before you start recording — don't waste time on `dotnet run` during the video
- Speak to the *why* of each decision, not just the *what* — the panel is evaluating architectural thinking
