# LgymApi.Payments.csproj

## Purpose and topology

- Purpose: provider-neutral Payments capability boundary with a private Stripe Checkout test-mode spike.
- Solution topology: the solution has 22 projects and 116 direct project-reference edges. Payments directly references only Domain and Identity. API composes Payments; Infrastructure, ArchitectureTests, IntegrationTests, UnitTests, and Payments.UnitTests consume its focused persistence contracts. The authoritative graph is `docs/modular-monolith/issue-380-project-reference-graph.md`.
- Rules: do not add direct references to API, Application, Infrastructure, Worker, Notifications, Platform, Resources, or HTTP adapters. Stripe SDK types, event names, signatures, raw payloads, secrets, and provider-specific workflow types remain private under `Providers/Stripe`.

## Persistence and workflow

- Payments owns `PaymentCheckout` and `PaymentEventReceipt` logically, but uses the one shared `AppDbContext`, PostgreSQL database, schema model, design-time factory, and Infrastructure migration stream. Migration `20261027200628_AddPaymentsPersistence` adds the tables and unique account/idempotency-key, provider-session-reference, and provider-event-ID constraints.
- Repositories are stage-only. The internal `IPaymentsWorkflowUnitOfWork` delegates to canonical `IUnitOfWork`; the Payments persistence context exposes no save or transaction operation.
- Checkout creation trims the client idempotency key, scopes it to the authenticated account, and derives Stripe's idempotency key from the account/key pair. A local checkout is committed before remote session creation; retries recover the same provider session. If Stripe succeeds but the later local save fails, the remote session can remain orphaned. The stable idempotency key makes retry recovery possible, but reconciliation is intentionally out of scope.
- A valid test-mode `checkout.session.completed` webhook is processed once. The raw body must be at most 256 KiB and have a current Stripe signature (300-second tolerance). The server retrieves the session and requires test mode, `payment_status=paid`, the configured price, quantity one, and matching checkout metadata before the only monotonic `Created -> Paid` transition. Duplicate or irrelevant verified events return success without another transition.

## API and client responsibility

- `POST /api/payments/checkout` is authenticated and protected by authenticated-user idempotency. It accepts no client-controlled payment fields and returns only a local checkout ID and hosted Checkout URL.
- `GET /api/payments/checkouts/{checkoutId}` is authenticated and owner-scoped. A non-owner receives `404`.
- `POST /api/payments/webhooks/stripe` is anonymous only because Stripe delivers it. It verifies the exact raw request body and Stripe signature; browser success/cancel returns are presentation-only and never decide payment state.
- Clients open the server-provided hosted URL and poll their own checkout status. They never choose price, quantity, currency, return URL, payment status, or account identity.

## Test-mode configuration and proof

- `Payments:Enabled` defaults to `false`. When enabled, hosting must not be Production; `Payments:ApiKey` must be `sk_test_...`; `Payments:PriceId` and absolute `Payments:SuccessUrl`/`Payments:CancelUrl` are required; and `Payments:WebhookSigningSecret` must be `whsec_...`.
- Inject Stripe test credentials, the signing secret, PostgreSQL connection string, JWT signing key, and a JWT for a pre-created disposable test account through user secrets, an untracked environment file, or the deployment secret store. Never commit them or pass them as script arguments.
- `scripts/run-payments-stripe-sandbox-proof.ps1` is a non-interactive proof gate. With supplied test-only prerequisites, including `psql`, it starts a local API and Stripe CLI listener, creates a hosted checkout, drives the Stripe test card flow with Playwright using a unique email asserted from the retrieved Stripe event, waits for `Paid`, replays the signed event, verifies one paid checkout and one processed receipt in PostgreSQL, and rejects real supplied sensitive values from captured API stdout/stderr. Without valid prerequisites it writes `BLOCKED` and exits nonzero; it never uses fixtures as a substitute.

## Rollout, rollback, and non-goals

- Deploy the Payments migration through the existing Infrastructure migration stream before enabling the spike. Roll back application and schema together using the normal reviewed migration rollback process; do not delete provider-side Checkout Sessions. Disabling Payments stops new workflow use but cannot undo an already-created external session or webhook delivery.
- This spike does not process real money or grant an entitlement, role, policy, authorization, or feature-access effect. It has no Worker jobs, subscriptions, refunds, disputes, taxes, invoices, customer portal, generic provider abstraction, raw card/token storage, or live mode.
- Apple and Google payment obligations are conditional on the final digital product, client platform, distribution region, and current store policy. They are not implemented or certified here. Polish/EU payment and consumer-law compliance is a GitHub [#491](https://github.com/withelm/LGYM-APP-APIv3/issues/491) follow-up, not achieved compliance.
