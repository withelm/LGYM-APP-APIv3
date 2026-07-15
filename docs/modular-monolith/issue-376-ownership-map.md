# Issue #376: Ownership Map

## Status
Complete

## Source precedence

- `#311` is the authority for ownership rules.
- `#375` is the factual inventory source for current artifacts.
- `docs/ARCHITECTURE.md` is the integration guide that will point to the final map.
- ADR-006 states the modular-monolith decision this map serves.

## Scope

This file contains the fixed one-owner matrix for the #375 hotspot and cross-feature inventory, plus the shared runtime roots called out in the issue #376 plan. One artifact, one owner module.

## Owner matrix

| Artifact type | Artifact name | Owner module | Justification | Allowed non-owner access mode |
| --- | --- | --- | --- | --- |
| Runtime root | `Program.cs` | `Platform / Reference Data` | The host composition root wires the whole runtime and owns startup registration. | Other modules do not register services here. They consume the host through DI and public endpoints only. |
| Runtime root | `LgymApi.Application/ServiceCollectionExtensions.cs` | `Platform / Reference Data` | Application registration is a shared startup concern and not a feature-owned decision. | Non-owner modules reference the registered services through DI. They do not add registrations here. |
| Runtime root | `LgymApi.Infrastructure/ServiceCollectionExtensions.cs` | `Platform / Reference Data` | Infrastructure registration owns the shared technical wiring for repositories, UoW, storage, and external adapters. | Non-owner modules use the registered technical services through DI only. |
| Runtime root | `LgymApi.BackgroundWorker/ServiceProvider.cs` | `Platform / Reference Data` | The background worker host is part of the shared runtime wiring surface. | Non-owner modules schedule work through the public worker contracts, not by editing this host. |
| Shared persistence root | `AppDbContext` | `Platform / Reference Data` | The single production EF Core context is the shared persistence root for the current monolith. | Non-owners only reach it through owner repositories and owner services. They do not create alternate context roots. |
| Shared contract | `IUnitOfWork` | `Platform / Reference Data` | Commit timing is a shared runtime rule that stays outside feature ownership. | Non-owner modules receive it only at service boundaries. Repositories do not own commits. |
| Shared contract | `ICommandDispatcher` | `Platform / Reference Data` | Command dispatch is a cross-feature runtime concern used by several modules. | Non-owners dispatch commands through the interface only. They do not embed dispatch logic in repositories or controllers. |
| Shared contract | `IGridifyExecutionService` | `Platform / Reference Data` | Query paging and filtering support is shared technical infrastructure. | Non-owner modules call it through the public interface. They do not own its registration or implementation. |
| Shared contract | `IMapperRegistry` | `Platform / Reference Data` | Mapper registration is a shared technical concern that supports the current mapping system. | Non-owner modules consume registered mappings only. They do not create competing registries. |
| Service family | `UserService*` | `Identity & Accounts` | User login, logout, profile, ranking, and onboarding flows belong to the identity boundary. | Other modules call the user service or its public contracts. They do not touch user state directly. |
| Dependency bag | `IUserServiceDependencies` | `Identity & Accounts` | The dependency bag belongs to the same user boundary as the service family it feeds. | Non-owners do not construct the bag. They use the user service contract instead. |
| Repository contract | `IUserRepository` | `Identity & Accounts` | User lookup and account ownership sit inside the identity boundary. | Non-owners query users through the identity service or a read contract, not by direct repository access. |
| Repository contract | `IRoleRepository` | `Identity & Accounts` | Roles are part of the account and authorization surface. | Non-owners read role data through service contracts only. |
| Repository contract | `IEloRegistryRepository` | `Identity & Accounts` | ELO registry data is tied to user identity and ranking state. | Non-owners use the owning services or read models instead of direct repository calls. |
| Service contract | `ITokenService` | `Identity & Accounts` | Token creation and validation are part of authentication. | Non-owners consume tokens through the auth flow, not by calling internals. |
| Service contract | `ILegacyPasswordService` | `Identity & Accounts` | Legacy password verification is an auth boundary concern. | Non-owners use the identity service flow rather than calling password internals. |
| Service contract | `IRankService` | `Identity & Accounts` | Rank calculation is exposed with the account surface and user state. | Non-owners request rank data through the identity service or read models. |
| Service contract | `IUserSessionStore` | `Identity & Accounts` | Session state belongs to the user boundary and its auth lifecycle. | Non-owners use the public auth flow and session contracts only. |
| Service contract | `ITutorialService` | `Identity & Accounts` | Onboarding and tutorial state are part of the user experience owned by identity. | Non-owners consume the public tutorial flow, not the persistence layer. |
| Table | `Users` | `Identity & Accounts` | The user table is the root account record for the identity boundary. | Non-owners read it through user-facing services and never write it directly. |
| Table | `EloRegistries` | `Identity & Accounts` | ELO registry rows track account ranking state. | Non-owners use the owning services or read models only. |
| Seed constant | `RoleSeedDataConfiguration.TesterRoleSeedId` | `Identity & Accounts` | The tester role seed identifier supports account bootstrap and user lookup rules. | Non-owners consume it only through the identity seed path and tests. |
| Service family | `InAppNotificationService*` | `Notifications` | In-app notification persistence and fan-out are notification concerns. | Non-owners call the notification service or published contracts only. |
| Dependency bag | `IInAppNotificationServiceDependencies` | `Notifications` | The dependency bag belongs to the in-app notification service family. | Non-owners do not construct the bag or bypass the service. |
| Repository contract | `IInAppNotificationRepository` | `Notifications` | In-app notification rows are notification-owned persisted state. | Non-owners consume notifications through the public service or read model. |
| Publisher contract | `IInAppNotificationPushPublisher` | `Notifications` | Push fan-out from in-app notifications belongs to the notifications boundary. | Non-owners publish only through the notification service workflow. |
| Bridge contract | `INotificationEventBridge` | `Notifications` | Event bridging for notification fan-out is notification-owned orchestration. | Non-owners receive events through the bridge contract only. |
| Service family | `PushNotificationService` | `Notifications` | Push enqueueing, deduplication, and message lifecycle belong to notifications. | Non-owners enqueue through the service contract only. |
| Repository contract | `PushInstallationRepository / IPushInstallationRepository` | `Notifications` | Installation ownership and stale cleanup are notification state. | Non-owners interact through registration and cleanup services, not direct writes. |
| Repository contract | `PushNotificationMessageRepository / IPushNotificationMessageRepository` | `Notifications` | Push message lifecycle and retry claims belong to notifications. | Non-owners consume push status through the service contract only. |
| Scheduler contract | `IPushBackgroundScheduler` | `Notifications` | Background push scheduling is part of the notification delivery boundary. | Non-owners schedule delivery through the public notification workflow only. |
| Service family | `StalePushInstallationCleanupService` | `Notifications` | Stale-installation cleanup is notification maintenance, not a shared utility. | Non-owners trigger it only through the worker contract and not via direct table edits. |
| Table | `PushInstallations` | `Notifications` | Installation rows are owned by notification registration and cleanup. | Non-owners never mutate the table directly. They use the notification service flow. |
| Table | `PushNotificationMessages` | `Notifications` | Push message rows are owned by notification delivery. | Non-owners only observe message state through the service contract or read model. |
| Service family | `ReportingService*` | `Reporting` | Report templates, requests, submissions, and photo handling belong to reporting. | Non-owners use the reporting service or published contracts only. |
| Dependency bag | `IReportingServiceDependencies` | `Reporting` | The dependency bag belongs to the reporting service family. | Non-owners do not compose the bag themselves. |
| Service family | `RecurringReportAssignmentService*` | `Reporting` | Recurring assignment processing is part of reporting orchestration. | Non-owners call the reporting service layer, not the repository set directly. |
| Dependency bag | `IRecurringReportAssignmentServiceDependencies` | `Reporting` | The dependency bag belongs to the recurring assignment service family. | Non-owners do not construct the bag. |
| Repository contract | `IReportingRepository` | `Reporting` | Reporting templates, requests, submissions, and photos are reporting-owned persisted state. | Non-owners use the reporting service or read models instead of direct repository access. |
| Repository contract | `IRecurringReportAssignmentRepository` | `Reporting` | Recurring assignment rows belong to reporting orchestration. | Non-owners reach them only through the reporting service. |
| Service contract | `IPhotoStorageProvider` | `Reporting` | Photo storage is part of the reporting photo workflow. | Non-owners consume it through the reporting upload flow only. |
| Service contract | `IPhotoUploadInitTracker` | `Reporting` | Upload init tracking belongs to reporting because it gates report photo uploads. | Non-owners do not track upload state directly. |
| Service contract | `IReportSubmissionMeasurementWriter` | `Reporting` | Measurement writes during report submission are part of reporting. | Non-owners write measurements through the reporting service only. |
| Table | `ReportTemplates` | `Reporting` | Templates are owned by reporting. | Non-owners read templates through reporting contracts only. |
| Table | `ReportRequests` | `Reporting` | Report requests are owned by reporting. | Non-owners do not write requests directly. |
| Table | `ReportSubmissions` | `Reporting` | Report submissions are owned by reporting. | Non-owners consume submission state through reporting contracts only. |
| Table | `Photos` | `Reporting` | Report photos are owned by reporting because the photo flow is part of report submission. | Non-owners use the reporting photo workflow only. |
| Service family | `PlanService` | `Training Planning` | Plan ownership and sharing belong to the planning boundary. | Non-owners consume plans through the planning service or read models only. |
| Repository contract | `PlanRepository / IPlanRepository` | `Training Planning` | Plan persistence is owned by the planning boundary. | Non-owners use the planning service, not the repository directly. |
| Repository contract | `IPlanDayRepository` | `Training Planning` | Plan day persistence belongs to the planning boundary. | Non-owners access plan days through the planning service only. |
| Table | `Plans` | `Training Planning` | Plan rows are owned by the planning boundary. | Non-owners do not write plans directly. |
| Service family | `TrainingService*` | `Workout & Progress` | Training, scoring, and workout progress belong to the workout boundary. | Non-owners call the training service or read model only. |
| Dependency bag | `ITrainingServiceDependencies` | `Workout & Progress` | The dependency bag belongs to the training service family. | Non-owners do not compose the bag themselves. |
| Repository contract | `IGymRepository` | `Workout & Progress` | Gym data is part of workout and progress tracking. | Non-owners query gyms through the workout service or read model. |
| Repository contract | `TrainingRepository / ITrainingRepository` | `Workout & Progress` | Training history is owned by the workout boundary. | Non-owners use the training service or read model only. |
| Repository contract | `IExerciseRepository` | `Workout & Progress` | Exercise lookup is part of workout and progress. | Non-owners use workout-facing service contracts instead of direct repository calls. |
| Repository contract | `IExerciseScoreRepository` | `Workout & Progress` | Exercise score persistence belongs to workout progress. | Non-owners read scores through the workout service or score read models. |
| Repository contract | `ITrainingExerciseScoreRepository` | `Workout & Progress` | Training exercise score persistence belongs to workout progress. | Non-owners use the workout service only. |
| Service contract | `IExerciseEloCalculator` | `Workout & Progress` | ELO calculation for workout performance belongs to the workout boundary. | Non-owners consume the calculator only through workout services. |
| Table | `Trainings` | `Workout & Progress` | Training rows are owned by workout and progress. | Non-owners do not write training rows directly. |
| Service family | `TrainerRelationshipService*` | `Coaching` | Trainer and trainee workflows belong to the coaching boundary. | Non-owners call the coaching service or published contracts only. |
| Dependency bag | `ITrainerRelationshipServiceDependencies` | `Coaching` | The dependency bag belongs to the trainer relationship service family. | Non-owners do not compose the bag themselves. |
| Repository contract | `TrainerRelationshipRepository / ITrainerRelationshipRepository` | `Coaching` | Trainer invitation and pairing persistence belongs to coaching. | Non-owners use coaching service contracts only. |
| Service contract | `ITrainingService` | `Workout & Progress` | Training is the workout boundary, even when coaching depends on it. | Non-owners call the training service interface only. |
| Service contract | `IExerciseScoresService` | `Coaching` | Coaching workflows aggregate exercise scores for trainer relations. | Non-owners consume it through coaching services or read models. |
| Service contract | `IEloRegistryService` | `Coaching` | Coaching workflows use ELO registry state to support trainer relations. | Non-owners reach it through the coaching service boundary only. |
| Service contract | `IMainRecordsService` | `Coaching` | Main record coordination belongs to the coaching workflow that consumes it. | Non-owners use the coaching service or read model only. |
| Repository contract | `MainRecordRepository / IMainRecordRepository` | `Coaching` | The current main-record repository contract is exposed through the coaching-owned main-record workflow and is treated as coaching-owned modular-boundary debt until the underlying ownership is extracted behind a cleaner published contract. | Non-owners use the coaching service or published read model only. |
| Table | `TrainerInvitations` | `Coaching` | Trainer invitations are owned by coaching. | Non-owners do not mutate invitations directly. |
| Table | `TrainerTraineeLinks` | `Coaching` | Trainer to trainee links are owned by coaching. | Non-owners do not mutate links directly. |
| Table | `DietPlans` | `Nutrition` | Diet plan rows are owned by nutrition. | Non-owners do not write diet plans directly. |
| Table | `DietMeals` | `Nutrition` | Diet meal rows are owned by nutrition. | Non-owners do not write diet meals directly. |
| Table | `DietPlanHistories` | `Nutrition` | Diet plan history rows are owned by nutrition. | Non-owners do not write diet plan history directly. |
| Table | `SupplementPlans` | `Nutrition` | Supplement plan rows are owned by nutrition. | Non-owners do not write supplement plans directly. |
| Table | `SupplementPlanItems` | `Nutrition` | Supplement plan item rows are owned by nutrition. | Non-owners do not write supplement plan items directly. |
| Table | `SupplementIntakeLogs` | `Nutrition` | Supplement intake log rows are owned by nutrition. | Non-owners do not write supplement intake logs directly. |

## Notes

- No placeholder rows remain.
- The owner choice is deterministic, not advisory.
- If an artifact can be used by another module, that module uses the owner service, contract, or read model.
- A non-owner never gets write authority just because the solution still uses one production `AppDbContext`.

## Ownership tie-breaker

If an artifact appears to touch more than one module, the owner is the module that owns the write boundary for that artifact. If the artifact is a shared runtime contract, the owner is `Platform / Reference Data`.

## Links

- `docs/adr/006-lgym-evolves-as-modular-monolith.md`
- `docs/modular-monolith/issue-376-module-context-map.md`
