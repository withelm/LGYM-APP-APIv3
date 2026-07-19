# LgymApi.Domain.csproj

- Purpose: core domain model.
- Contains: entities, enums, strongly typed IDs, domain helpers, and auth/security constants.
- Rules: keep free of HTTP, EF, and API concerns.
- Boundary: do not reorder or renumber existing enums.
- `Exercise` now carries `ExerciseEloFormula` with `Standard` as the default profile.
- `ExerciseEloFormula.PullupWeighted` rewards lower weight for pull-up style exercises where added weight makes the score worse.
- `PushInstallation` stores installation-scoped FCM registration state with optional user/session binding so logout and account-switch flows can disassociate a device without deleting its installation record.
- `CommandEnvelope.CommandTypeFullName` retains its legacy property/column name but stores the canonical legacy `LgymApi.BackgroundWorker.Common.Commands.*` command ID rather than an arbitrary CLR full name. Application CLR names are read aliases only and are never persisted as the canonical ID.
