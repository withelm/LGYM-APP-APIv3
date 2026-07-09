# LgymApi.Domain.csproj

- Purpose: core domain model.
- Contains: entities, enums, strongly typed IDs, domain helpers, and auth/security constants.
- Rules: keep free of HTTP, EF, and API concerns.
- Boundary: do not reorder or renumber existing enums.
- `Exercise` now carries `ExerciseEloFormula` with `Standard` as the default profile.
- `ExerciseEloFormula.PullupWeighted` rewards lower weight for pull-up style exercises where added weight makes the score worse.
