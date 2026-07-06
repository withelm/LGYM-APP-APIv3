# LgymApi.BackgroundWorker.Common.csproj

- Purpose: shared contracts for background work.
- Contains: job contracts, serialization helpers, DI abstractions, and notification/job models.
- Rules: put cross-boundary worker contracts here, not HTTP/controller code.
- Boundary: keep this module reusable from worker and application sides.
