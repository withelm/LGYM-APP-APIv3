namespace LgymApi.ArchitectureTests;

public static class ModuleBoundaryDebtAllowlistRegistry
{
    private static readonly IReadOnlyList<ModuleBoundaryDebtEntry> Entries =
    [
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "LgymApi.Application.Features.User.IUserServiceDependencies @ LgymApi.Application/User/IUserServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository @ LgymApi.Application/Repositories/IPushInstallationRepository.cs",
            rationale: "Current modular-boundary debt: identity user-session flows still depend on notifications push-installation persistence contracts.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "LgymApi.Application.Features.User.UserService @ LgymApi.Application/User/UserService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository @ LgymApi.Application/Repositories/IPushInstallationRepository.cs",
            rationale: "Current modular-boundary debt: identity user-session flows still depend on notifications push-installation persistence contracts.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "LgymApi.Application.Features.User.UserServiceDependencies @ LgymApi.Application/User/IUserServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository @ LgymApi.Application/Repositories/IPushInstallationRepository.cs",
            rationale: "Current modular-boundary debt: identity user-session flows still depend on notifications push-installation persistence contracts.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.DietPlans.DietPlanService @ LgymApi.Application/Features/DietPlans/DietPlanService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: nutrition plan assignment still reaches coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.Supplementation.SupplementationService @ LgymApi.Application/Features/Supplementation/SupplementationService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: nutrition plan assignment still reaches coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Platform / Reference Data",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.AppConfig.AppConfigService @ LgymApi.Application/AppConfig/AppConfigService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository @ LgymApi.Application/Repositories/IRoleRepository.cs",
            rationale: "Current modular-boundary debt: platform app-config reads still query identity-owned role data directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Platform / Reference Data",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.AppConfig.AppConfigService @ LgymApi.Application/AppConfig/AppConfigService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository @ LgymApi.Application/Repositories/IUserRepository.cs",
            rationale: "Current modular-boundary debt: platform app-config reads still query identity-owned user data directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.Reporting.ReportSubmissionMeasurementWriter @ LgymApi.Application/Features/Reporting/ReportSubmissionMeasurementWriter.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IMeasurementRepository @ LgymApi.Application/Repositories/IMeasurementRepository.cs",
            rationale: "Current modular-boundary debt: report submission still writes workout/progress measurements through direct repository access.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.IPlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayService @ LgymApi.Application/PlanDay/PlanDayService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.IPlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository @ LgymApi.Application/Repositories/IExerciseRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.IPlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository @ LgymApi.Application/Repositories/ITrainingRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayService @ LgymApi.Application/PlanDay/PlanDayService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository @ LgymApi.Application/Repositories/IExerciseRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayService @ LgymApi.Application/PlanDay/PlanDayService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository @ LgymApi.Application/Repositories/ITrainingRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository @ LgymApi.Application/Repositories/IExerciseRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.PlanDay.PlanDayServiceDependencies @ LgymApi.Application/PlanDay/IPlanDayServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository @ LgymApi.Application/Repositories/ITrainingRepository.cs",
            rationale: "Current modular-boundary debt: training-planning plan-day flows still depend on workout/progress repositories directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.Measurements.IMeasurementsServiceDependencies @ LgymApi.Application/Measurements/IMeasurementsServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: workout/progress measurement flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.Measurements.MeasurementsService @ LgymApi.Application/Measurements/MeasurementsService.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: workout/progress measurement flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "ModuleDependencyGuardTests",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "LgymApi.Application.Features.Measurements.MeasurementsServiceDependencies @ LgymApi.Application/Measurements/IMeasurementsServiceDependencies.cs",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository @ LgymApi.Application/Repositories/ITrainerRelationshipRepository.cs",
            rationale: "Current modular-boundary debt: workout/progress measurement flows still depend on coaching relationship persistence directly.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AcceptInvitationAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddHistoryEntryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AssignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AssignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ChangedByUserId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateInvitationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateInvitationAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateInvitationByEmailAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateInvitationByEmailAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateTrainerNoteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteTrainerNoteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DetachFromTrainerAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureOwnedNoteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetActiveAssignedPlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetCurrentTrainerAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetCurrentTrainerAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetDashboardTraineesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetInvitationForTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetInvitationsPaginatedAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeEloChartAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeExerciseScoresChartDataAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeMainRecordsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineePlansAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeTrainingByDateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeTrainingDatesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainerInvitationsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainerNoteHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainerNotesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetVisibleNoteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetVisibleNotesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "Id",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LastUpdatedByUserId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/TraineeNotes/ITraineeNoteService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/TraineeNotes/Models/TraineeNoteModels.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/TraineeNotes/TraineeNoteService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/ITrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/Models/TraineeTrainerProfileResult.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Dashboard.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.InvitationCreation.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.InvitationLifecycle.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Links.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Plans.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.TraineeProfile.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapHistory",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapInvitation",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapNote",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "NotifyNoteUpdatedAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RejectInvitationAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RevokeInvitationAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RoleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TraineeId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerRelationshipServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerRelationshipServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerRelationshipService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerRelationshipService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UnassignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UnassignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UnlinkTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateTrainerNoteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UserRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_roleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_userRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "AssignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "AssignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "CreateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "CreateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "DeleteTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "DeleteTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "GetActiveAssignedPlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "GetTraineePlansAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "Id",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/ITrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Plans.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "MapPlan",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "PlanRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "TrainerRelationshipServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "TrainerRelationshipService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "UnassignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "UpdateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "UpdateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Plan",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "_planRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPlanRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetTraineeExerciseScoresChartDataAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetTraineeMainRecordsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/ITrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/ITrainerRelationshipService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Dashboard.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/TrainerRelationships/TrainerRelationshipService.Dashboard.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "AddNewRecordAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "AddNewRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "AddNewRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "DeleteMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseMap",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseScoreRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseScoreRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetBestRecord",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetLastMainRecordsAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetLastMainRecordsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetLastMainRecordsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetMainRecordsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetRecordOrPossibleRecordInExerciseAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseScoreRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetRecordOrPossibleRecordInExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetRecordOrPossibleRecordInExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.MainRecords.Models.AddMainRecordInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.MainRecords.Models.UpdateMainRecordInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application.Features.MainRecords.Models.UpdateMainRecordInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/MainRecords/IMainRecordsService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/MainRecords/MainRecordsService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/MainRecords/MainRecordsService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/MainRecords/Models/MainRecordsLastContext.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/MainRecords/Models/MainRecordsLastContext.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "MainRecordsServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "MainRecordsServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseScoreRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "MainRecordsService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "MainRecordsService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseScoreRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "Records",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "UpdateMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "UpdateMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "UpdateMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.MainRecord",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "_exerciseRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "_exerciseScoreRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseScoreRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "DisassociateInstallationsForSessionAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "DisassociatePushInstallationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "GetBoundInstallationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "PushInstallationRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "RegisterPushInstallationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "UnregisterPushInstallationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "UserServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "UserService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Identity & Accounts",
            targetModule: "Notifications",
            sourceSymbolOrPath: "_pushInstallationRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IPushInstallationRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "DietPlanService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "SupplementationService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Coaching",
            sourceSymbolOrPath: "_trainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ActivateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddHistoryEntryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AssignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ChangedByUserId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CheckOffIntakeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateHistoryEntry",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureOwnedPlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetActiveScheduleForDateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetComplianceSummaryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetCurrentPlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetCurrentPlansAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineePlanHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineePlansAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/DietPlans/DietPlanMapping.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/DietPlans/DietPlanService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/DietPlans/IDietPlanService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/DietPlans/Models/DietPlanModels.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Supplementation/ISupplementationService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Supplementation/SupplementationService.Compliance.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Supplementation/SupplementationService.PlanManagement.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Supplementation/SupplementationService.TraineeSchedule.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Supplementation/SupplementationService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapHistory",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapPlan",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "NotifyDietPlanUpdatedAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "SupplementationService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TraineeId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UnassignTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateTraineePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Nutrition",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_roleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "RecurringReportAssignmentServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "RecurringReportAssignmentService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "ReportingServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "ReportingService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "TrainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "ValidatePhotoAccessAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Coaching",
            sourceSymbolOrPath: "_trainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "BuildStorageKeyPrefix",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CompletePhotoUploadAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CountRecentUploadInitsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateAndPersistPhotoAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateReportRequestAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateTemplateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteTemplateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureOwnedTemplateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureStorageKeyHasExpectedPrefix",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EnsureTrainerOwnsTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GenerateStorageKey",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetForTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetOwnSubmissionsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetOwnedAssignmentAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPendingRequestsForTraineeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPhotoHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetSignedReadUrlAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTraineeSubmissionsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainerTemplateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainerTemplatesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "InitiatePhotoUploadAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "InitiatedByUserId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/IRecurringReportAssignmentService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/IReportSubmissionMeasurementWriter.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/IReportingService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/RecurringReportAssignmentService.Support.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/RecurringReportAssignmentService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportSubmissionMeasurementWriter.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Photos.Read.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Photos.Refactor.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Photos.Support.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Photos.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Requests.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Submissions.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.Templates.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportingService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapAssignment",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapRequest",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapSubmission",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MapTemplate",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MarkTrainerFeedbackAsReadAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "OwnerUserId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "PauseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RecurringReportAssignmentServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RecurringReportAssignmentService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ReportingServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ReportingService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ResumeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RoleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "StageMeasurementsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "SubmitReportRequestAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TraineeId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainerId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateTemplateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateTrainerFeedbackAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidateCompletePhotoUploadRequestAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidateDeveloperLimitsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidatePendingUpload",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidatePhotoAccessAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidatePhotoAccessAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidateTrainerAndCommandAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_roleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/Features/Reporting/ReportSubmissionMeasurementWriter.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Measurement",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ReportSubmissionMeasurementWriter",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IMeasurementRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "StageMeasurementsAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IMeasurementRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "StageMeasurementsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Measurement",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Reporting",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "_measurementRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IMeasurementRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "CanAccessPlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "PlanDayServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "PlanDayService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "TrainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Coaching",
            sourceSymbolOrPath: "_trainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CanAccessPlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CheckIsUserHavePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CopyPlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreatePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreatePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreatePlanDayAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeletePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeletePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeletePlanDayAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GenerateShareCodeAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlanConfigAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlanDayAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlanDaysAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlanDaysInfoAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlanDaysTypesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetPlansListAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Plan/IPlanService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Plan/PlanService.Crud.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Plan/PlanService.Lifecycle.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Plan/PlanService.Sharing.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Plan/PlanService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/IPlanDayService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/PlanDayService.Mutations.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/PlanDayService.Queries.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/PlanDayService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "PlanService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "SetNewActivePlanAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "SetNewActivePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdatePlanAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdatePlanDayAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_userRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseMap",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "ExerciseRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetPlanDayAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetPlanDayAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetPlanDaysAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetPlanDaysAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "GetPlanDaysInfoAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/Models/PlanDayDetailsContext.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "LgymApi.Application/PlanDay/Models/PlanDaysContext.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "PlanDayServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "PlanDayServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "PlanDayService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "PlanDayService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "TrainingRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "Translations",
            targetSymbolOrPath: "LgymApi.Domain.Entities.Exercise",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "_exerciseRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IExerciseRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Training Planning",
            targetModule: "Workout & Progress",
            sourceSymbolOrPath: "_trainingRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainingRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "MeasurementsServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "MeasurementsService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "TrainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "ValidateAccessAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Coaching",
            sourceSymbolOrPath: "_trainerRelationshipRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.ITrainerRelationshipRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddGlobalTranslationAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddGlobalTranslationAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddGymAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddMeasurementAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddMeasurementsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddNewRecordAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddNewRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddTrainingAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IEloRegistryRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddTrainingAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddTrainingAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.EloRegistry",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddTrainingAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddUserExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "AddUserExerciseWithFormulaAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateExerciseAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "CreateExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteExerciseAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteExerciseAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteGymAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "DeleteMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "EloRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IEloRegistryRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ExerciseScoresService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ExerciseService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ExerciseService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "FetchPreviousScores",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetAllExercisesAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetAllExercisesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetAllUserExercisesAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetAllUserExercisesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetExerciseByBodyPartAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetExerciseByBodyPartAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetExerciseScoresChartDataAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetExerciseScoresChartDataAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetExerciseScoresFromTrainingByExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetGymAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetGymsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetLastExerciseScoresAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetLastMainRecordsAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetLastMainRecordsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetLastTrainingAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetLastTrainingAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMainRecordsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMainRecordsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementDetailAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementsHistoryAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementsInternalAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementsListAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementsTrendAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetMeasurementsTrendsAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetRecordOrPossibleRecordInExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainingByDateAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainingByDateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "GetTrainingDatesAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.Exercise.ExerciseService.UpdateExerciseRequest",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.Exercise.Models.AddGlobalTranslationInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.Exercise.Models.AddUserExerciseInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.Exercise.Models.AddUserExerciseWithFormulaInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.Exercise.Models.GetLastExerciseScoresInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.MainRecords.Models.AddMainRecordInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application.Features.MainRecords.Models.UpdateMainRecordInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Exercise/ExerciseService.Management.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Exercise/ExerciseService.Queries.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Exercise/ExerciseService.Scores.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Exercise/IExerciseService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Gym/GymService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Gym/IGymService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Measurements/IMeasurementsService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Measurements/MeasurementsService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "LgymApi.Application/Training/ITrainingService.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MainRecordsServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MainRecordsService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MeasurementsServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "MeasurementsService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "RoleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainingServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IEloRegistryRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainingServiceDependencies",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainingService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IEloRegistryRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "TrainingService",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateExerciseAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateExerciseCoreAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateExerciseCoreAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateExerciseWithFormulaAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateGymAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Coaching",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UpdateMainRecordAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "UserRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidateAccessAsync",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "ValidateAccessAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.User",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_eloRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IEloRegistryRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_roleRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IRoleRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Identity & Accounts",
            sourceSymbolOrPath: "_userRepository",
            targetSymbolOrPath: "LgymApi.Application.Repositories.IUserRepository",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "AddTrainingAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "GetTrainingByDateAsync",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "LgymApi.Application.Features.Training.Models.AddTrainingInput",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "LgymApi.Application/Training/Models/TrainingByDateDetails.cs",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "PlanDay",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced.")),
        new ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey.Create(
            guardId: "CrossModuleEntityLeakage",
            sourceModule: "Workout & Progress",
            targetModule: "Training Planning",
            sourceSymbolOrPath: "TypePlanDayId",
            targetSymbolOrPath: "LgymApi.Domain.Entities.PlanDay",
            rationale: "Pre-existing cross-module entity/repository coupling tracked as shrink-only debt while explicit contracts/read models/events are introduced."))
    ];

    public static IReadOnlyList<ModuleBoundaryDebtEntry> AllEntries => Entries;

    public static IReadOnlyList<ModuleBoundaryDebtEntry> GetEntriesForGuard(string guardId)
    {
        var normalizedGuardId = ModuleBoundaryDebtKey.NormalizeRequiredValue(guardId, nameof(guardId));

        return Entries
            .Where(entry => entry.Key.GuardId.Equals(normalizedGuardId, StringComparison.Ordinal))
            .ToList();
    }

    public static ModuleBoundaryDebtAllowlistEvaluation Evaluate(string guardId, IEnumerable<ModuleBoundaryObservedViolation> observedViolations)
    {
        ArgumentNullException.ThrowIfNull(observedViolations);

        var normalizedGuardId = ModuleBoundaryDebtKey.NormalizeRequiredValue(guardId, nameof(guardId));
        var allowlistedEntries = GetEntriesForGuard(normalizedGuardId);

        return ModuleBoundaryDebtAllowlistEvaluator.Evaluate(allowlistedEntries, observedViolations, normalizedGuardId);
    }

    public static void AssertNoUnexpectedViolations(string guardId, IEnumerable<ModuleBoundaryObservedViolation> observedViolations)
    {
        var evaluation = Evaluate(guardId, observedViolations);
        if (evaluation.IsSuccess)
        {
            return;
        }

        throw new AssertionException(evaluation.BuildFailureMessage());
    }
}

public static class ModuleBoundaryDebtAllowlistEvaluator
{
    public static ModuleBoundaryDebtAllowlistEvaluation Evaluate(
        IEnumerable<ModuleBoundaryDebtEntry> allowlistedEntries,
        IEnumerable<ModuleBoundaryObservedViolation> observedViolations,
        string? guardId = null)
    {
        ArgumentNullException.ThrowIfNull(allowlistedEntries);
        ArgumentNullException.ThrowIfNull(observedViolations);

        var entries = allowlistedEntries.ToList();
        var observed = observedViolations.ToList();
        var normalizedGuardId = NormalizeGuardId(guardId, entries, observed);

        ValidateEntries(entries, normalizedGuardId);
        ValidateObservedViolations(observed, normalizedGuardId);

        var matchedObservedKeys = observed
            .Select(violation => violation.IdentityKey)
            .ToHashSet(StringComparer.Ordinal);

        var unexpectedViolations = observed
            .Where(violation => entries.All(entry => !entry.Matches(violation)))
            .OrderBy(violation => violation.IdentityKey, StringComparer.Ordinal)
            .ToList();

        var staleEntries = entries
            .Where(entry => !matchedObservedKeys.Contains(entry.IdentityKey))
            .OrderBy(entry => entry.IdentityKey, StringComparer.Ordinal)
            .ToList();

        return new ModuleBoundaryDebtAllowlistEvaluation(normalizedGuardId, unexpectedViolations, staleEntries);
    }

    private static void ValidateEntries(IReadOnlyCollection<ModuleBoundaryDebtEntry> entries, string normalizedGuardId)
    {
        var duplicateEntryKeys = entries
            .GroupBy(entry => entry.Key.NormalizedKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (duplicateEntryKeys.Count > 0)
        {
            throw new AssertionException(
                $"Module-boundary debt allowlist for guard '{normalizedGuardId}' contains duplicate exact entries:{Environment.NewLine}" +
                string.Join(Environment.NewLine, duplicateEntryKeys));
        }

        var duplicateIdentityKeys = entries
            .GroupBy(entry => entry.IdentityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (duplicateIdentityKeys.Count > 0)
        {
            throw new AssertionException(
                $"Module-boundary debt allowlist for guard '{normalizedGuardId}' contains duplicate identity matches. Keep one exact entry per live violation:{Environment.NewLine}" +
                string.Join(Environment.NewLine, duplicateIdentityKeys));
        }
    }

    private static void ValidateObservedViolations(IReadOnlyCollection<ModuleBoundaryObservedViolation> observedViolations, string normalizedGuardId)
    {
        var invalidGuardViolations = observedViolations
            .Where(violation => !violation.GuardId.Equals(normalizedGuardId, StringComparison.Ordinal))
            .Select(violation => violation.IdentityKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (invalidGuardViolations.Count > 0)
        {
            throw new AssertionException(
                $"Observed module-boundary violations must use the requested guard id '{normalizedGuardId}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, invalidGuardViolations));
        }
    }

    private static string NormalizeGuardId(
        string? guardId,
        IReadOnlyCollection<ModuleBoundaryDebtEntry> entries,
        IReadOnlyCollection<ModuleBoundaryObservedViolation> observedViolations)
    {
        if (!string.IsNullOrWhiteSpace(guardId))
        {
            return ModuleBoundaryDebtKey.NormalizeRequiredValue(guardId, nameof(guardId));
        }

        var discoveredGuardId = entries.Select(entry => entry.Key.GuardId)
            .Concat(observedViolations.Select(violation => violation.GuardId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return discoveredGuardId.Count switch
        {
            0 => throw new AssertionException("Module-boundary debt allowlist evaluation requires a guard id when no entries or observed violations are present."),
            1 => discoveredGuardId[0],
            _ => throw new AssertionException(
                "Module-boundary debt allowlist evaluation requires exactly one guard id. Found:" + Environment.NewLine +
                string.Join(Environment.NewLine, discoveredGuardId.OrderBy(id => id, StringComparer.Ordinal)))
        };
    }
}

public sealed record ModuleBoundaryDebtAllowlistEvaluation(
    string GuardId,
    IReadOnlyList<ModuleBoundaryObservedViolation> UnexpectedViolations,
    IReadOnlyList<ModuleBoundaryDebtEntry> StaleEntries)
{
    public bool IsSuccess => UnexpectedViolations.Count == 0 && StaleEntries.Count == 0;

    public string BuildFailureMessage()
    {
        var sections = new List<string>();

        if (UnexpectedViolations.Count > 0)
        {
            sections.Add(
                "New module-boundary violations must be fixed or explicitly allowlisted as exact current debt:" + Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine, UnexpectedViolations.Select(violation => violation.ToString())));
        }

        if (StaleEntries.Count > 0)
        {
            sections.Add(
                "Stale module-boundary allowlist entries must be removed once the live violation disappears:" + Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine, StaleEntries.Select(entry => entry.ToString())));
        }

        return $"Module-boundary shrink-only debt allowlist failed for guard '{GuardId}'.{Environment.NewLine}" +
               string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
}

public sealed record ModuleBoundaryDebtEntry(ModuleBoundaryDebtKey Key)
{
    public string IdentityKey => Key.IdentityKey;

    public bool Matches(ModuleBoundaryObservedViolation observedViolation)
    {
        ArgumentNullException.ThrowIfNull(observedViolation);
        return IdentityKey.Equals(observedViolation.IdentityKey, StringComparison.Ordinal);
    }

    public override string ToString() => Key.ToDisplayString();
}

public sealed record ModuleBoundaryObservedViolation(
    string GuardId,
    string SourceModule,
    string TargetModule,
    string SourceSymbolOrPath,
    string TargetSymbolOrPath)
{
    public string IdentityKey => ModuleBoundaryDebtKey.BuildIdentityKey(GuardId, SourceModule, TargetModule, SourceSymbolOrPath, TargetSymbolOrPath);

    public override string ToString()
    {
        return $"Rule: {GuardId}{Environment.NewLine}" +
               $"Source module: {SourceModule}{Environment.NewLine}" +
               $"Target module: {TargetModule}{Environment.NewLine}" +
               $"Source symbol/file: {SourceSymbolOrPath}{Environment.NewLine}" +
               $"Target symbol/file: {TargetSymbolOrPath}";
    }
}

public sealed record ModuleBoundaryDebtKey(
    string GuardId,
    string SourceModule,
    string TargetModule,
    string SourceSymbolOrPath,
    string TargetSymbolOrPath,
    string Rationale)
{
    public string IdentityKey => BuildIdentityKey(GuardId, SourceModule, TargetModule, SourceSymbolOrPath, TargetSymbolOrPath);

    public string NormalizedKey => $"{IdentityKey}|rationale:{Rationale}";

    public string ToDisplayString()
    {
        return $"Rule: {GuardId}{Environment.NewLine}" +
               $"Source module: {SourceModule}{Environment.NewLine}" +
               $"Target module: {TargetModule}{Environment.NewLine}" +
               $"Source symbol/file: {SourceSymbolOrPath}{Environment.NewLine}" +
               $"Target symbol/file: {TargetSymbolOrPath}{Environment.NewLine}" +
               $"Rationale: {Rationale}";
    }

    public static ModuleBoundaryDebtKey Create(
        string guardId,
        string sourceModule,
        string targetModule,
        string sourceSymbolOrPath,
        string targetSymbolOrPath,
        string rationale)
    {
        return new ModuleBoundaryDebtKey(
            NormalizeRequiredValue(guardId, nameof(guardId)),
            NormalizeRequiredValue(sourceModule, nameof(sourceModule)),
            NormalizeRequiredValue(targetModule, nameof(targetModule)),
            NormalizeRequiredPathOrSymbol(sourceSymbolOrPath, nameof(sourceSymbolOrPath)),
            NormalizeRequiredPathOrSymbol(targetSymbolOrPath, nameof(targetSymbolOrPath)),
            NormalizeRequiredValue(rationale, nameof(rationale)));
    }

    public static string BuildIdentityKey(
        string guardId,
        string sourceModule,
        string targetModule,
        string sourceSymbolOrPath,
        string targetSymbolOrPath)
    {
        return string.Join(
            "|",
            $"guard:{NormalizeRequiredValue(guardId, nameof(guardId))}",
            $"source-module:{NormalizeRequiredValue(sourceModule, nameof(sourceModule))}",
            $"target-module:{NormalizeRequiredValue(targetModule, nameof(targetModule))}",
            $"source:{NormalizeRequiredPathOrSymbol(sourceSymbolOrPath, nameof(sourceSymbolOrPath))}",
            $"target:{NormalizeRequiredPathOrSymbol(targetSymbolOrPath, nameof(targetSymbolOrPath))}");
    }

    public static string NormalizeRequiredPathOrSymbol(string value, string paramName)
    {
        return NormalizeRequiredValue(ArchitectureTestHelpers.NormalizePath(value), paramName);
    }

    public static string NormalizeRequiredValue(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }
}
