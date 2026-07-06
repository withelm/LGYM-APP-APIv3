# LgymApi.Application.csproj

- Purpose: use-case and business orchestration layer.
- Contains: services, service interfaces, repository abstractions, application models, mapping core, notification abstractions, and app DI.
- Rules: own business rules, authorization checks, transactions, and UoW commits here.
- Boundary: do not reference infrastructure implementations.
