using System.ComponentModel.DataAnnotations;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly ITokenService _tokenService;
    private readonly ILegacyPasswordService _legacyPasswordService;
    private readonly IRankService _rankService;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        IUserRepository userRepository,
        IEloRegistryRepository eloRepository,
        ITokenService tokenService,
        ILegacyPasswordService legacyPasswordService,
        IRankService rankService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _eloRepository = eloRepository;
        _tokenService = tokenService;
        _legacyPasswordService = legacyPasswordService;
        _rankService = rankService;
        _unitOfWork = unitOfWork;
    }

    public async Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.NotFound(Messages.NameIsRequired);
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw AppException.NotFound(Messages.EmailInvalid);
        }

        if (password.Length < 6)
        {
            throw AppException.NotFound(Messages.PasswordMin);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw AppException.NotFound(Messages.SamePassword);
        }

        var existingUser = await _userRepository.FindByNameOrEmailAsync(name, normalizedEmail!, cancellationToken);
        if (existingUser != null)
        {
            if (string.Equals(existingUser.Name, name, StringComparison.Ordinal))
            {
                throw AppException.NotFound(Messages.UserWithThatName);
            }

            throw AppException.NotFound(Messages.UserWithThatEmail);
        }

        var passwordData = _legacyPasswordService.Create(password);
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Admin = false,
            Email = normalizedEmail!,
            IsVisibleInRanking = isVisibleInRanking ?? true,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _eloRepository.AddAsync(new global::LgymApi.Domain.Entities.EloRegistry
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = DateTimeOffset.UtcNow,
            Elo = 1000
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var user = await _userRepository.FindByNameAsync(name, cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.LegacyHash) || string.IsNullOrWhiteSpace(user.LegacySalt))
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var valid = _legacyPasswordService.Verify(
            password,
            user.LegacyHash,
            user.LegacySalt,
            user.LegacyIterations,
            user.LegacyKeyLength,
            user.LegacyDigest);

        if (!valid)
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var token = _tokenService.CreateToken(user.Id);
        var elo = await _eloRepository.GetLatestEloAsync(user.Id, cancellationToken) ?? 1000;
        var nextRank = _rankService.GetNextRank(user.ProfileRank);

        return new LoginResult
        {
            Token = token,
            User = new UserInfoResult
            {
                Name = user.Name,
                Id = user.Id,
                Email = user.Email,
                Avatar = user.Avatar,
                Admin = user.Admin,
                ProfileRank = user.ProfileRank,
                CreatedAt = user.CreatedAt.UtcDateTime,
                UpdatedAt = user.UpdatedAt.UtcDateTime,
                Elo = elo,
                NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                IsDeleted = user.IsDeleted,
                IsTester = user.IsTester,
                IsVisibleInRanking = user.IsVisibleInRanking
            }
        };
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        return user != null && user.Admin == true;
    }

    public async Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var nextRank = _rankService.GetNextRank(currentUser.ProfileRank);
        var elo = await _eloRepository.GetLatestEloAsync(currentUser.Id, cancellationToken) ?? 1000;

        return new UserInfoResult
        {
            Name = currentUser.Name,
            Id = currentUser.Id,
            Email = currentUser.Email,
            Avatar = currentUser.Avatar,
            Admin = currentUser.Admin,
            ProfileRank = currentUser.ProfileRank,
            CreatedAt = currentUser.CreatedAt.UtcDateTime,
            UpdatedAt = currentUser.UpdatedAt.UtcDateTime,
            Elo = elo,
            NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
            IsDeleted = currentUser.IsDeleted,
            IsTester = currentUser.IsTester,
            IsVisibleInRanking = currentUser.IsVisibleInRanking
        };
    }

    public async Task<List<RankingEntry>> GetUsersRankingAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetRankingAsync(cancellationToken);
        if (users.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return users.Select(u => new RankingEntry
        {
            Name = u.User.Name,
            Avatar = u.User.Avatar,
            Elo = u.Elo,
            ProfileRank = u.User.ProfileRank
        }).ToList();
    }

    public async Task<int> GetUserEloAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var result = await _eloRepository.GetLatestEloAsync(userId, cancellationToken);
        if (!result.HasValue)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return result.Value;
    }

    public async Task DeleteAccountAsync(UserEntity currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        currentUser.Email = $"anonymized_{currentUser.Id}@example.com";
        currentUser.Name = $"anonymized_user_{currentUser.Id}";
        currentUser.IsDeleted = true;

        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.BadRequest(Messages.DidntFind);
        }

        currentUser.IsVisibleInRanking = isVisibleInRanking;
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
