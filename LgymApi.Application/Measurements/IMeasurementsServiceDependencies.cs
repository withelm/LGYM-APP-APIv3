using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Measurements;

public interface IMeasurementsServiceDependencies
{
    IMeasurementRepository MeasurementRepository { get; }
    IRoleRepository RoleRepository { get; }
    ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    IUnitConverter<HeightUnits> HeightUnitConverter { get; }
    IUnitConverter<WeightUnits> WeightUnitConverter { get; }
    IUnitOfWork UnitOfWork { get; }
}

internal sealed class MeasurementsServiceDependencies : IMeasurementsServiceDependencies
{
    public MeasurementsServiceDependencies(
        IMeasurementRepository measurementRepository,
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IUnitConverter<HeightUnits> heightUnitConverter,
        IUnitConverter<WeightUnits> weightUnitConverter,
        IUnitOfWork unitOfWork)
    {
        MeasurementRepository = measurementRepository;
        RoleRepository = roleRepository;
        TrainerRelationshipRepository = trainerRelationshipRepository;
        HeightUnitConverter = heightUnitConverter;
        WeightUnitConverter = weightUnitConverter;
        UnitOfWork = unitOfWork;
    }

    public IMeasurementRepository MeasurementRepository { get; }
    public IRoleRepository RoleRepository { get; }
    public ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    public IUnitConverter<HeightUnits> HeightUnitConverter { get; }
    public IUnitConverter<WeightUnits> WeightUnitConverter { get; }
    public IUnitOfWork UnitOfWork { get; }
}
