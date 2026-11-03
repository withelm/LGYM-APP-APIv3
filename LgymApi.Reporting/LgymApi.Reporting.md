# LgymApi.Reporting.csproj

- Purpose: Reporting use cases, focused persistence ports, API adapters, reporting mapping profiles, and owner-facing registration facade.
- Compatibility: source retains established `LgymApi.Application.Reporting.*` and `LgymApi.Application.Features.Reporting.*` namespaces so API, Worker, persisted command, and mapper contracts are unchanged.
- Dependencies: direct references are limited to Domain, Identity, Platform, and Resources; Reporting must not reference Application, API, Infrastructure, Worker, Coaching, or Training Planning.
- Persistence: Infrastructure supplies Reporting's five stage-only adapters and owns the sole `AppDbContext`, database, schema, and migration stream.
- Composition: `AddReportingModule()` registers Reporting services and `AddReportingApiAdapters()` registers its API-facing adapters; the API host composes both through this facade.
- Testing: `LgymApi.Reporting.UnitTests` is the friend test assembly for Reporting-owned service, persistence, recurring-assignment, mapping, and photo tests.
- Photo reads: signed reads, request/trainee history, and submission hydration bind each active photo to its authoritative non-deleted source request and trainee owner before metadata projection or original/thumbnail URL signing. The trainee and currently active source trainer may read; successors, former trainers, and privileged roles without source ownership receive no photo entry or signed capability.
