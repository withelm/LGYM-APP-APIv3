using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
public sealed partial class TrainerRelationshipController : ControllerBase
{
    private readonly ITrainerRelationshipService _trainerRelationshipService;
    private readonly IMapper _mapper;

    public TrainerRelationshipController(ITrainerRelationshipService trainerRelationshipService, IMapper mapper)
    {
        _trainerRelationshipService = trainerRelationshipService;
        _mapper = mapper;
    }
}
