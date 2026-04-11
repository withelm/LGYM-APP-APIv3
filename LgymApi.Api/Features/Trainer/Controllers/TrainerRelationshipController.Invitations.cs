using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Trainer.Controllers;

public sealed partial class TrainerRelationshipController : ControllerBase
{
    [HttpPost("invitations")]
    [ApiIdempotency("/api/trainer/invitations", ApiIdempotencyScopeSource.AuthenticatedUser)]
    [ProducesResponseType(typeof(TrainerInvitationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateTrainerInvitationRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(request.TraineeId, out var traineeId))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.CreateInvitationAsync(trainer!, traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(result.Value));
    }

    [HttpPost("invitations/paginated")]
    [ProducesResponseType(typeof(PaginatedTrainerInvitationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitationsPaginated([FromBody] PaginatedTrainerInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var filterInput = new FilterInput
        {
            Page = request.Page,
            PageSize = request.PageSize,
            FilterGroups = request.FilterGroups,
            SortDescriptors = request.SortDescriptors
        };

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetInvitationsPaginatedAsync(trainer!, filterInput, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var pagination = result.Value;
        var response = new PaginatedTrainerInvitationResult
        {
            Items = _mapper.MapList<TrainerInvitationResult, TrainerInvitationDto>(pagination.Items),
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = pagination.TotalCount,
            TotalPages = pagination.TotalPages,
            HasNextPage = pagination.HasNextPage,
            HasPreviousPage = pagination.HasPreviousPage
        };
        return Ok(response);
    }

    [HttpPost("invitations/by-email")]
    [ProducesResponseType(typeof(TrainerInvitationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvitationByEmail([FromBody] CreateTrainerInvitationByEmailRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.CreateInvitationByEmailAsync(
            trainer!, request.Email, request.PreferredLanguage, request.PreferredTimeZone, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(result.Value));
    }

    [HttpPost("invitations/{invitationId}/revoke")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeInvitation([FromRoute] string invitationId, CancellationToken cancellationToken = default)
    {
        if (!Id<TrainerInvitationEntity>.TryParse(invitationId, out var parsedInvitationId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.RevokeInvitationAsync(trainer!, parsedInvitationId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
