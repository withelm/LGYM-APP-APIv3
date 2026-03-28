

## 2026-03-28 Task 8: API layer Guid migration (COMPLETED)

### Scope
- Migrated all API middleware, controllers, validators from direct `Guid` usage to `Id<TEntity>` abstractions
- Files modified: 1 middleware + 18 controllers + 2 validators = 21 files total
- Eliminated ~75 direct Guid token occurrences in LgymApi.Api layer

### Migration Pattern
- **Controllers**: Replaced `Guid.TryParse(routeParam, out var id)` + `(Id<TEntity>)id` casts with direct `Id<TEntity>.TryParse(routeParam, out var id)`
- **Middleware**: JWT claim parsing in `UserContextMiddleware` migrated from `Guid.TryParse` to `Id<User>.TryParse`
- **Validators**: FluentValidation `.Must()` rules migrated to use `Id<TEntity>.TryParse` instead of `Guid.TryParse`
- **Mappers**: No changes required (already compatible with typed IDs)

### Files Migrated

#### Middleware (1 file)
- `LgymApi.Api/Middleware/UserContextMiddleware.cs`: JWT claim parsing now uses `Id<User>.TryParse`

#### Controllers (18 files)
- `LgymApi.Api/Features/Exercise/Controllers/ExerciseController.cs` (9 methods)
- `LgymApi.Api/Features/Gym/Controllers/GymController.cs` (5 methods)
- `LgymApi.Api/Features/Role/Controllers/RoleController.cs` (5 methods)
- `LgymApi.Api/Features/Plan/Controllers/PlanController.cs` (8 methods)
- `LgymApi.Api/Features/PlanDay/Controllers/PlanDayController.cs` (7 methods)
- `LgymApi.Api/Features/Measurements/Controllers/MeasurementsController.cs` (4 methods)
- `LgymApi.Api/Features/Trainer/Controllers/TrainerRelationshipController.cs` (14 methods)
- `LgymApi.Api/Features/Trainer/Controllers/TrainerSupplementationController.cs` (7 methods)
- `LgymApi.Api/Features/Trainer/Controllers/TrainerReportingController.cs` (6 methods)
- `LgymApi.Api/Features/Trainer/Controllers/TraineeRelationshipController.cs` (2 methods)
- `LgymApi.Api/Features/Trainer/Controllers/TraineeSupplementationController.cs` (1 method)
- `LgymApi.Api/Features/Trainer/Controllers/TraineeReportingController.cs` (1 method)
- `LgymApi.Api/Features/User/Controllers/UserController.cs` (2 methods with ID parsing)
- `LgymApi.Api/Features/Training/Controllers/TrainingController.cs` (4 methods)
- `LgymApi.Api/Features/MainRecords/Controllers/MainRecordsController.cs` (1 remaining cast removed)

#### Validators (2 files)
- `LgymApi.Api/Features/Trainer/Validation/CreateReportRequestRequestValidator.cs`: `Id<ReportTemplate>.TryParse`
- `LgymApi.Api/Features/Trainer/Validation/CreateTrainerInvitationRequestValidator.cs`: `Id<User>.TryParse`

### Verification Evidence
- **Build status**: `dotnet build LgymApi.sln --configuration Release --no-restore` passes with 0 errors (only pre-existing warnings: MimeKit NU1902 package vulnerability, CS8604 nullable reference type warnings in 4 files, RS1034 Roslyn analyzer hints in ArchitectureTests)
- **Grep scan**: `grep -r '\bGuid\b' LgymApi.Api/**/*.cs` returns 0 matches
- **API contract preservation**: All endpoint signatures and response formats unchanged; only internal parsing logic migrated from `Guid` → `Id<TEntity>`

### Pattern Observations
- Controllers consistently followed parse-then-cast pattern: `Guid.TryParse` → `(Id<TEntity>)` → typed ID variable
- Direct replacement with `Id<TEntity>.TryParse` eliminates cast and centralizes parse semantics in `Id.cs`
- `HttpContext.ParseRouteUserIdForCurrentUser` extension already returns typed `Id<User>`, eliminating many redundant casts after migration
- Empty fallback pattern changed from `Guid.Empty` cast to direct `Id<TEntity>.Empty` usage
- FluentValidation rules required `using LgymApi.Domain.ValueObjects;` addition for typed ID parse methods

### Task 8 Status
**COMPLETED** - All API layer files (middleware, controllers, validators) migrated from direct Guid to typed IDs. Zero handwritten Guid usage remains in LgymApi.Api layer.

