# ADR-006: LGYM evolves as a modular monolith

## Status
Accepted

## Context

LGYM already has a layered runtime and a single production deployable backed by one PostgreSQL database and one production `AppDbContext`.

Issue #375 captured the current-state inventory, and issue #376 turns that inventory into a modular-monolith contract so later work can define boundaries without changing the deployment shape.

## Source precedence

- `#311` is the constraint authority.
- `#375` is the factual baseline and inventory source.
- `docs/ARCHITECTURE.md` is the integration target and reader guide.
- This ADR records the decision layer only and must stay aligned with those sources.

## Decision

LGYM evolves as a modular monolith.

The current system stays as one deployable, one PostgreSQL database, one production `AppDbContext`, and one migration stream.

The modular-monolith decision changes how the system is described and governed, not the current runtime shape in this issue.

## Rationale

1. The #375 baseline already shows stable feature clusters that can be governed as modules.
2. #311 requires the modular-monolith direction to preserve the current runtime constraints while boundaries are defined.
3. Keeping the deployable, database, `AppDbContext`, and migration stream intact avoids inventing topology work that does not belong in this issue.
4. A modular-monolith contract lets the later module docs define ownership, dependency direction, and communication rules without changing the layered runtime.
5. The ADR keeps `docs/ARCHITECTURE.md` and the module docs aligned so the current system description stays consistent across the repo.

## Consequences

1. Later issue-376 docs must stay consistent with the seven-module catalog, the ownership map, and the dependency policy matrix.
2. Ownership rules remain one-owner-per-artifact, with no shared write ownership hidden behind the one production `AppDbContext`.
3. The current layered runtime remains the implementation baseline; this ADR does not authorize any change to the current deployment or persistence shape.
4. The single migration stream remains the only migration history for the current production system.
5. `docs/ARCHITECTURE.md` can point at this ADR and the companion module docs as the durable modular-monolith references.

## Follow-up

1. Keep the module context map aligned with #311 and #375.
2. Keep the ownership map aligned with the module catalog and dependency policy.
3. Update `docs/ARCHITECTURE.md` to link the finalized modular-monolith docs.

## Links

- `docs/modular-monolith/issue-376-module-context-map.md`
- `docs/modular-monolith/issue-376-ownership-map.md`
