# ADR-008: Payments test-mode spike topology

## Status

Accepted

## Decision owner

LGYM API maintainers, with approval recorded for LGYM-13.

## Context

LGYM now has 19 projects, 97 direct project references, and nine logical owners. `LgymApi.Payments` establishes a standalone payment boundary without creating a second database or weakening the existing modular-monolith boundaries. This ADR records the completed test-mode spike boundary, not a production payments decision.

## Decision

LGYM implements `LgymApi.Payments` as the ninth canonical owner with a narrow provider-neutral persistence seam. The implemented topology is 19 projects and 97 direct edges: Payments references only Domain and Identity, API references Payments to compose its public facade, while Infrastructure, ArchitectureTests, IntegrationTests, and UnitTests reference its persistence contracts. Payments owns checkout and event-receipt entities in the shared migration stream and contains the private Stripe Checkout test-mode SDK boundary under `Providers/Stripe`. It has three thin API endpoints and no Worker integration: authenticated checkout initiation, owner-scoped checkout status, and an anonymous signature-verified Stripe webhook.

This ADR supersedes only its obsolete module-count clauses: the former 18-project/90-edge/eight-owner baseline and the statement that this commit did not create the project, solution entry, references, graph counts, or architecture guards. All other future-boundary and non-goal clauses remain in force.

The implemented Payments public surface is narrow and provider-neutral. Public contracts depend only on approved stable domain and module contracts needed by a compiling use case. Stripe SDK types, provider event names, secrets, signatures, raw payloads, and provider-specific persistence models remain private to Payments provider code. Payments must not depend directly on API, Application, Infrastructure, BackgroundWorker, BackgroundWorker.Common, or Notifications unless a later topology decision explicitly approves that dependency.

Payments uses the existing physical persistence topology: one `AppDbContext`, one PostgreSQL database, one schema model, one design-time factory, and one Infrastructure migration stream. Payments owns its persisted entities logically through the existing shared-context bridge and registrar. Its repositories remain stage-only, and its workflows own the unit-of-work commit boundary. The Payments project must not introduce a second context, database, schema, or migration stream.

The implemented Stripe Checkout spike is limited to test mode. `Payments:Enabled` remains false by default; an enabled instance rejects Production hosting and requires a server-injected `sk_test_` key, one configured test price, fixed absolute success/cancel URLs, and a `whsec_` signing secret. Credentials are supplied only through user secrets, untracked environment variables, or a deployment secret store, never tracked configuration or client input.

The private provider boundary accepts an authenticated account and normalized client idempotency key, persists a local checkout correlation record before creation, and reuses Stripe's derived idempotency key to recover a provider-success/local-save failure without duplicating the remote session. A browser return is presentation only. A local payment status may change only after a verified raw-body webhook is correlated to the local record and a server-retrieved Checkout Session reports test mode, `payment_status=paid`, the configured price, quantity one, and matching checkout metadata. Webhooks consume the exact raw body, enforce a 256 KiB limit and 300-second signature tolerance, record each provider event ID once, and treat a duplicate delivery as a successful no-op. The spike must be idempotent and must have no entitlement, role, authorization, or feature-access effect.

## Non-goals

- Live payment mode or real-money processing.
- Raw card data, payment tokens, secrets, signatures, or raw provider payload storage.
- Client-controlled price, quantity, currency, status, or return URLs.
- Subscriptions, refunds, disputes, taxes, invoices, customer portals, or recurring jobs.
- Entitlements, paid feature gates, role changes, authorization changes, or other product access effects.
- Apple StoreKit or Google Play Billing implementation, certification, or compliance claims.
- A generic payment-provider abstraction framework.
- A second persistence topology or a Payments-specific deployment.

## Consequences

The ADR gives later implementation work a clear owner and boundary. The topology, graph guards, persistence migration, and documentation are implemented atomically; later behavior must add only dependencies justified by compiling source evidence. Migration `20261027200628_AddPaymentsPersistence` remains in the shared Infrastructure stream. Roll back schema and application together through the reviewed migration procedure; disabling the spike or rolling back local data cannot revoke an already-created Stripe Checkout Session or external webhook delivery.

If this decision is reversed, it requires a separately approved atomic topology change that removes the Payments project, references, guards, and any future owned persistence artifacts while preserving the single shared persistence stream.

## Follow-up and compliance context

- LGYM-13 is the originating work item for the Payments spike.
- GitHub issue [#491](https://github.com/withelm/LGYM-APP-APIv3/issues/491) records the follow-up and compliance context for the future payment boundary.
- Apple and Google payment obligations depend on the final digital feature, client platform, distribution region, and current Apple/Google policy. This spike implements neither StoreKit nor Play Billing and makes no certification or compliance claim.
- Polish/EU payment, consumer-law, tax, and compliance work remains the GitHub #491 follow-up. This ADR does not claim achieved compliance.
- Before any production decision, define the product/access model and native-store policy, select a live-mode secret and webhook deployment model, add reconciliation for orphaned provider sessions, decide refunds/subscriptions/taxes/disputes/invoices/customer support, complete compliance review, and add production operational monitoring and incident response.

## Links

- `docs/adr/006-lgym-evolves-as-modular-monolith.md`
- `docs/adr/007-final-modular-monolith-compatibility-commitments.md`
- `docs/modular-monolith/issue-380-project-reference-graph.md`
- `docs/MODULE_CONTRIBUTION_GUIDE.md`
