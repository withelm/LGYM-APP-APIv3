using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Exercise;

public sealed partial class ExerciseService : IExerciseService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExerciseService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
        _unitOfWork = unitOfWork;
    }

    private async Task<Dictionary<Id<Domain.Entities.Exercise>, string>> GetTranslationsForExercisesAsync(IEnumerable<Domain.Entities.Exercise> exercises, IReadOnlyList<string> cultures, CancellationToken cancellationToken)
    {
        var globalIds = exercises
            .Where(e => e.UserId == null)
            .Select(e => e.Id)
            .ToList();

        if (globalIds.Count == 0)
        {
            return new Dictionary<Id<Domain.Entities.Exercise>, string>();
        }

        var translations = await _exerciseRepository.GetTranslationsAsync(globalIds, cultures, cancellationToken);
        return translations;
    }
}
