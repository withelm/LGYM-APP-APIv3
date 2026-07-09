# LgymApi.Api.csproj

- Purpose: ASP.NET Core HTTP entrypoint.
- Contains: controllers, DTO contracts, validators, middleware, mapping profiles, auth, JSON setup, Swagger, CORS, rate limits, SignalR, and composition root.
- Rules: keep controllers thin and preserve legacy payload shapes.
- Boundary: do not move application or infrastructure business logic here.
- Exercise read and legacy write contracts stay without `eloFormula`; privileged exercise create/update endpoints use separate DTOs and `ManageGlobalExercises` policy.
- Enum-backed choice lists must return `LookupItem`-style payloads with stable enum-string `id`; `name` and `displayName` both carry translated display text.
- Exercise ELO formulas must be sourced from the enum lookup API (`/api/enums/enumType/ExerciseEloFormula`) and must not be hardcoded in the front-end.
- Lookup-backed request values such as `ExerciseExtendedFormDto.EloFormula` must be mapped in API mapping profiles from lookup ids to application enums; controllers should not parse them.
