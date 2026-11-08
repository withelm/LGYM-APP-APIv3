using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.GetComplianceSummary;

public sealed record GetSupplementComplianceSummaryQuery(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    DateOnly FromDate,
    DateOnly ToDate);
