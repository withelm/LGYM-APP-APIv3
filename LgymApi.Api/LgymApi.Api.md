# LgymApi.Api.csproj

- Purpose: ASP.NET Core HTTP entrypoint.
- Contains: controllers, DTO contracts, validators, middleware, mapping profiles, auth, JSON setup, Swagger, CORS, rate limits, SignalR, and composition root.
- Rules: keep controllers thin and preserve legacy payload shapes.
- Boundary: do not move application or infrastructure business logic here.
- Exercise read and legacy write contracts stay without `eloFormula`; privileged exercise create/update endpoints use separate DTOs and `ManageGlobalExercises` policy.
- Enum-backed choice lists must return `LookupItem`-style payloads with stable enum-string `id`; `name` and `displayName` both carry translated display text.
- Exercise ELO formulas must be sourced from the enum lookup API (`/api/enums/enumType/ExerciseEloFormula`) and must not be hardcoded in the front-end.
- Lookup-backed request values such as `ExerciseExtendedFormDto.EloFormula` must be mapped in API mapping profiles from lookup ids to application enums; controllers should not parse them.
- Push installation endpoints live under `/api/push/installations/*`, rely on middleware-authenticated `User` plus `sid` claims, and never trust client-supplied user identity for device registration or disassociation.
- Push sender configuration follows the existing `appsettings.json` convention under `PushNotifications:*`; the API host composes the enqueue service and background worker pieces but does not send push notifications synchronously inside controllers.
- The only API-level push bridge path is the admin-only test endpoint at `/api/internal/push/test-event`; it accepts privacy-safe IDs only and exists to prove generic enqueue wiring without auto-converting all in-app notifications to push.
- Rollout guardrails rely on `PushNotifications:SendEnabled` for send-only disablement plus the recurring `push-stale-installation-cleanup` Hangfire job for inactive-token tombstoning.
