# Issue #376: Module Context Map

## Status
Draft

## Source precedence

- `#311` defines the constraints and allowed direction.
- `#375` provides the factual baseline for current module clusters.
- `docs/ARCHITECTURE.md` is the reader-facing integration guide.
- ADR-006 explains why the modular-monolith direction exists.

## Scope

This file defines the current module catalog and the draft policy notes that later issue-376 tasks will refine.

## Module catalog

The #375 baseline already shows these as the current feature-level dependency clusters, and #311 constrains how far the modular-monolith direction can go.

## Platform / Reference Data

### Responsibility boundary

- Owns host startup, composition root wiring, shared configuration, cross-cutting infrastructure hooks, and reference data that multiple modules depend on.
- Does not own end-user workflows, business decisions, or feature-specific write rules.

### Public contract surface

- Exposes shared bootstrapping, DI registration, lookup data, and other stable platform services that the feature modules consume.
- Publishes only cross-module primitives and lookup payloads, not feature commands or feature-specific read models.

## Identity & Accounts

### Responsibility boundary

- Owns authentication, account state, user identity, roles, permissions, sessions, and account-scoped lifecycle rules.
- Does not own training, reporting, workout, coaching, or notification business flows beyond the account events they consume.

### Public contract surface

- Exposes login, logout, profile, identity, and authorization contracts that other modules can rely on.
- Publishes account identity events and account lookup data, while keeping credential handling and account mutation rules internal.

## Notifications

### Responsibility boundary

- Owns in-app notifications, push registration, push delivery lifecycle, and stale-installation cleanup.
- Does not own the business event that triggered the message, only the delivery and audit of that message.

### Public contract surface

- Exposes notification submission, delivery status, and installation management contracts for other modules.
- Accepts domain events and notification requests from other modules, but keeps provider integration details internal.

## Reporting

### Responsibility boundary

- Owns report templates, report requests, report submissions, submission measurements, and report-photo workflows.
- Does not own training plans, workout scoring, or coaching relationships, except where those inputs are required to validate reporting access.

### Public contract surface

- Exposes report request, submission, template, and photo-upload contracts to the rest of the system.
- Publishes reporting statuses, submission views, and report-related commands or events without leaking storage concerns.

## Training Planning

### Responsibility boundary

- Owns plan creation, plan editing, plan-day structure, and planning-time user ownership rules.
- Does not own workout execution history, score calculation, or coach-trainee relationship management.

### Public contract surface

- Exposes plan management commands, plan views, and plan-sharing contracts used by adjacent modules.
- Publishes planning data that other modules can read, while keeping plan mutation rules inside the module.

## Workout & Progress

### Responsibility boundary

- Owns workout sessions, exercise progress, score calculation, ELO or rank updates, and the history needed to track progress over time.
- Does not own planning, account identity, or coaching links, even when those inputs are needed to validate a workout action.

### Public contract surface

- Exposes workout entry, history, scoring, and progress contracts for the UI and neighboring modules.
- Publishes progress summaries, workout events, and score updates, while keeping calculation details internal.

## Coaching

### Responsibility boundary

- Owns trainer and trainee relationships, invitations, dashboard views, and the coordination layer between a coach and the people they support.
- Does not own identity, workout execution, reporting storage, or plan structure, even when it can initiate actions in those modules.

### Public contract surface

- Exposes coach-trainee relationship, invitation, and dashboard contracts that other modules can query or react to.
- Publishes coaching relationship changes and request flows, while delegating actual workout, plan, or report behavior to the owning module.

## Nutrition

### Responsibility boundary

- Owns diet plans, meals, supplement plans, plan history, and intake logs.
- Does not own identity, training planning, workout execution, reporting, or notification business flows beyond the shared events and contracts it consumes.

### Public contract surface

- Exposes diet-plan, meal, supplement-plan, and intake contracts that other modules can read or react to.
- Publishes nutrition data and nutrition events while keeping nutrition write rules internal.

## Allowed dependencies

The #375 baseline shows the current clusters already depend on shared platform, identity, and infrastructure roots. #311 narrows that into a contract-first module policy.

| Module | May depend on | Must not depend on |
| --- | --- | --- |
| Platform / Reference Data | Its own shared bootstrap, configuration, reference data stores, and external runtime primitives | Any feature module's internal services, write models, or feature workflows |
| Identity & Accounts | Platform / Reference Data | Internal persistence or private services from Notifications, Reporting, Training Planning, Workout & Progress, or Coaching |
| Notifications | Platform / Reference Data; Identity & Accounts public contracts | Internal persistence or private services from Reporting, Training Planning, Workout & Progress, or Coaching |
| Reporting | Platform / Reference Data; Identity & Accounts public contracts; Coaching relationship contracts; Training Planning read contracts | Internal persistence or private services from Notifications, Training Planning, Workout & Progress, or Coaching |
| Training Planning | Platform / Reference Data; Identity & Accounts public contracts | Internal persistence or private services from Notifications, Reporting, Workout & Progress, or Coaching |
| Workout & Progress | Platform / Reference Data; Identity & Accounts public contracts; Training Planning plan contracts | Internal persistence or private services from Notifications, Reporting, or Coaching |
| Coaching | Platform / Reference Data; Identity & Accounts public contracts; Training Planning; Workout & Progress; Notifications public contracts | Internal persistence or private services from Reporting |
| Nutrition | Platform / Reference Data; Identity & Accounts public contracts | Internal persistence or private services from Notifications, Reporting, Training Planning, Workout & Progress, or Coaching |

## Platform / Reference Data hosting rules

Platform / Reference Data may host application startup, composition-root wiring, shared configuration, reference data, lookup data, and other cross-cutting primitives that multiple modules consume.

It must not absorb feature-specific commands, business workflows, or module-owned write models, because those belong to the fixed feature modules and stay under their own contract surfaces.

## Forbidden dependencies

No module may skip its published contract and reach into another module's private state or implementation.
No module may use direct access to another module's repositories or direct access to another module's entities instead of the published contract surface.
No module may create a catch-all boundary or a side channel that replaces the owning module's contract.

## Cross-module communication rules

Cross-module communication must use one of these modes only: published contracts, read models, or in-process events that stay inside the application boundary.

- Published contracts are the default for request/response access between modules.
- Read models are allowed for non-owner reads when the target module exposes them as part of its public surface.
- In-process events are allowed only as module-local coordination or as published notifications between modules; they do not grant write access.

Any new interaction must name the source module, target module, and contract surface so later ownership mapping stays traceable to #375.
Direct persistence calls, shared repositories, and hidden side channels are not communication modes.

## Write ownership rules

Each entity or table has exactly one owner module.
Only that owner module may mutate the row set, enforce write validation, or change the write model.
Non-owner modules may read published views or call published contracts, but they may not mutate the underlying entity or table directly.
The solution still uses one production AppDbContext, and the production deployment still uses one database; those shared technical roots do not create shared write ownership.
Shared platform/reference data may be read by all modules, but only the owning module can change it.

## Extraction criteria

Extraction is justified only when all of these conditions are true:

- the module has a stable responsibility boundary that does not depend on private state from another module;
- the module's public-contract surface is complete enough for non-owners to use without repository access;
- the module's write ownership can stay exclusive to one owner module;
- the module no longer needs direct access to another module's repositories, entities, or private services;
- the dependency matrix can remain valid after extraction without adding a boundary-bypass path.

The eight-module catalog is the current baseline for those checks, and any later split must stay compatible with the #311 constraints.

## Links

- `docs/adr/006-lgym-evolves-as-modular-monolith.md`
- `docs/modular-monolith/issue-376-ownership-map.md`
