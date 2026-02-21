# GitHub Issue Labeling Guide

This document defines which labels are available in this repository and when to use them.

## Goals

- Keep backlog triage consistent.
- Make filtering by area, urgency, and scope predictable.
- Avoid label noise and contradictory tagging.

## Label Groups and Rules

Use labels from these groups.

- **Type**: exactly one per issue.
- **Area**: at least one per issue when applicable.
- **Priority**: at most one per issue.
- **Program/Scope**: optional, use when issue belongs to a tracked initiative.
- **Workflow/Meta**: optional, use only when situation matches the label meaning.

## 1) Type Labels (choose exactly one)

- `bug`: Something is broken or behavior is incorrect.
- `enhancement`: Improvement, refactor, or new capability request.
- `documentation`: Docs-only work or documentation as the primary output.
- `question`: Clarification request that is not yet actionable engineering work.

## 2) Area Labels (choose one or more)

- `backend`: API, application services, middleware, auth, business logic.
- `database`: Schema, migrations, persistence model, SQL/EF data access concerns.
- `testing`: Unit/integration/e2e tests, test infrastructure, regression coverage.
- `architecture`: Cross-cutting design, module boundaries, patterns, contracts.
- `refactor`: Internal code cleanup/restructure with no intentional behavior change.
- `performance`: Throughput, latency, memory, query/runtime optimization.

## 3) Priority Labels (choose at most one)

- `priority:high`: Urgent or risky issue; should be scheduled early.
- `priority:medium`: Important but not urgent; normal planning lane.

If no priority label is set, issue is treated as default priority.

## 4) Program/Scope Labels (optional)

- `feature`: Parent/top-level feature tracking item.
- `mvp`: In current MVP scope.
- `post-mvp`: Explicitly out of MVP scope.
- `trainer-api`: Belongs to LGYM Trainer API initiative.

Do not combine `mvp` and `post-mvp` on the same issue.

## 5) Workflow/Meta Labels (use only when true)

- `help wanted`: Team asks for external/extra contributor attention.
- `good first issue`: Safe and scoped task suitable for onboarding.
- `duplicate`: Same as an existing issue.
- `invalid`: Not actionable or based on incorrect assumptions.
- `wontfix`: Intentionally not planned for implementation.

## Recommended Labeling Pattern

For most engineering tasks, use:

1. One **Type** label.
2. One or more **Area** labels.
3. Optional **Priority** label.
4. Optional **Program/Scope** label(s).

Example combinations:

- API bug in training endpoint: `bug`, `backend`, `priority:high`
- EF migration task: `enhancement`, `database`, `backend`
- Architecture cleanup with no behavior change: `enhancement`, `architecture`, `refactor`
- Regression coverage task: `enhancement`, `testing`
- Trainer initiative milestone item: `enhancement`, `backend`, `trainer-api`, `mvp`

## Anti-Patterns

- Adding both `bug` and `enhancement`.
- Adding both `mvp` and `post-mvp`.
- Using `documentation` when code delivery is the main objective.
- Over-labeling with unrelated areas to increase visibility.

## Triage Checklist

When creating or reviewing an issue:

1. Confirm one correct **Type** label.
2. Add relevant **Area** labels.
3. Set **Priority** only if it changes scheduling.
4. Add **Program/Scope** labels if issue belongs to tracked initiative.
5. Remove stale or conflicting labels.
