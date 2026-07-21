using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.WorkoutProgress.Dashboard;
using System;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipServiceDependencies
{
    IUserRepository UserRepository { get; }
    IRoleRepository RoleRepository { get; }
    ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    IPlanRepository PlanRepository { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IWorkoutProgressDashboardReadService WorkoutProgressDashboardReadService { get; }
    IUnitOfWork UnitOfWork { get; }
    IServiceProvider ServiceProvider { get; }
    ILogger<TrainerRelationshipService> Logger { get; }
}

internal sealed class TrainerRelationshipServiceDependencies : ITrainerRelationshipServiceDependencies
{
    public TrainerRelationshipServiceDependencies(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IPlanRepository planRepository,
        ICommandDispatcher commandDispatcher,
        IWorkoutProgressDashboardReadService workoutProgressDashboardReadService,
        IUnitOfWork unitOfWork,
        IServiceProvider serviceProvider,
        ILogger<TrainerRelationshipService> logger)
    {
        UserRepository = userRepository;
        RoleRepository = roleRepository;
        TrainerRelationshipRepository = trainerRelationshipRepository;
        PlanRepository = planRepository;
        CommandDispatcher = commandDispatcher;
        WorkoutProgressDashboardReadService = workoutProgressDashboardReadService;
        UnitOfWork = unitOfWork;
        ServiceProvider = serviceProvider;
        Logger = logger;
    }

    public IUserRepository UserRepository { get; }
    public IRoleRepository RoleRepository { get; }
    public ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    public IPlanRepository PlanRepository { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IWorkoutProgressDashboardReadService WorkoutProgressDashboardReadService { get; }
    public IUnitOfWork UnitOfWork { get; }
    public IServiceProvider ServiceProvider { get; }
    public ILogger<TrainerRelationshipService> Logger { get; }
}
