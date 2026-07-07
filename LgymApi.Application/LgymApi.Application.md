# LgymApi.Application.csproj

- Purpose: use-case and business orchestration layer.
- Contains: services, service interfaces, repository abstractions, application models, mapping core, notification abstractions, and app DI.
- Rules: own business rules, authorization checks, transactions, and UoW commits here.
- Boundary: do not reference infrastructure implementations.
- Training ELO scoring is selected by exercise profile in the training service; legacy exercise create/update methods always use the standard profile while privileged variants can opt into alternate formulas.
- Training ELO formulas are implemented as one strategy class per `ExerciseEloFormula` under `Application/Common/Training/Elo` and resolved through DI instead of switch logic in `TrainingService`; the pull-up profile rewards lower weight with `PullupWeighted`.
- Enum lookups are the source of truth for front-end choice lists; `ExerciseEloFormula` is exposed through the enum lookup service and should not be duplicated as a hardcoded UI list.
