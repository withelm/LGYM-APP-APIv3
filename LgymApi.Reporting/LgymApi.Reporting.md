# LgymApi.Reporting.csproj

- Purpose: Reporting use cases, focused persistence ports, API adapters, reporting mapping profiles, and owner-facing registration facade.
- Compatibility: source retains established `LgymApi.Application.Reporting.*` and `LgymApi.Application.Features.Reporting.*` namespaces so API, Worker, persisted command, and mapper contracts are unchanged.
- Dependencies: direct references are limited to Domain, Identity, Platform, and Resources; Reporting must not reference Application, API, Infrastructure, Worker, Coaching, or Training Planning.
- Persistence: Infrastructure supplies Reporting's five stage-only adapters and owns the sole `AppDbContext`, database, schema, and migration stream.
- Composition: `AddReportingModule()` registers Reporting services and `AddReportingApiAdapters()` registers its seven API-facing adapters; the API host composes both through this facade. Reporting owns the `IPhotoStorageProvider` abstraction and evidence lifecycle policy. Infrastructure owns private photo-provider implementations, option binding and validation, provider selection, and DI registration.
- Testing: `LgymApi.Reporting.UnitTests` is the friend test assembly for Reporting-owned service, persistence, recurring-assignment, mapping, and photo tests.
