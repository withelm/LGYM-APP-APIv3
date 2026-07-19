using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.Ranking;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Identity.Ranking;

public sealed class UserRankingService : IUserRankingService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UserRankingService(IUserRepository userRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetRankingAsync(cancellationToken);
        if (users.Count == 0)
        {
            return Result<List<RankingEntry>, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        return Result<List<RankingEntry>, AppError>.Success(_mapper.MapList<UserRankingEntry, RankingEntry>(users));
    }

    public async Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(
        UserEntity? currentUser,
        bool isVisibleInRanking,
        CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.DidntFind));
        }

        currentUser.IsVisibleInRanking = isVisibleInRanking;
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
