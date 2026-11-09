using LgymApi.Api.Middleware;
using LgymApi.Domain.ValueObjects;
using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Exercise.Controllers;

[ApiController]
[Route("api/exercise")]
[Authorize(Policy = AuthConstants.Policies.ManageGlobalExercises)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 400)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 401)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 403)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 404)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 409)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 410)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 422)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 503)]
public sealed class ExerciseMediaController(IExerciseMediaService service, IMapper mapper) : ControllerBase
{
    [HttpPost("media/uploads")]
    [ProducesResponseType(typeof(ExerciseMediaUploadDto), 200)]
    public async Task<IActionResult> InitiateExerciseMedia([FromBody] InitiateExerciseMediaDto request, CancellationToken cancellationToken)
    {
        var result = await service.InitiateAsync(HttpContext.GetAuthenticatedAccountContext(), mapper.Map<InitiateExerciseMediaDto, InitiateExerciseMediaInput>(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaUploadResult, ExerciseMediaUploadDto>(result.Value));
    }

    [HttpPost("media/uploads/{uploadReference}/verify")]
    [ProducesResponseType(typeof(ExerciseMediaVerificationDto), 200)]
    public async Task<IActionResult> VerifyExerciseMedia(string uploadReference, CancellationToken cancellationToken)
    {
        var result = await service.VerifyAsync(HttpContext.GetAuthenticatedAccountContext(), uploadReference.ToIdOrEmpty<ExerciseMediaUpload>(), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaVerificationResult, ExerciseMediaVerificationDto>(result.Value));
    }

    [HttpDelete("media/uploads/{uploadReference}")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> CancelExerciseMedia(string uploadReference, CancellationToken cancellationToken) => (await service.CancelAsync(HttpContext.GetAuthenticatedAccountContext(), uploadReference.ToIdOrEmpty<ExerciseMediaUpload>(), cancellationToken)).ToActionResult();

    [HttpPost("media/create")]
    [ProducesResponseType(typeof(ExerciseMediaMutationDto), 200)]
    public async Task<IActionResult> CreateExerciseWithMedia([FromBody] CreateExerciseMediaDto request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(HttpContext.GetAuthenticatedAccountContext(), mapper.Map<CreateExerciseMediaDto, CreateExerciseMediaInput>(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaMutationResult, ExerciseMediaMutationDto>(result.Value));
    }

    [HttpPatch("{id}/media")]
    [ProducesResponseType(typeof(ExerciseMediaMutationDto), 200)]
    public async Task<IActionResult> ChangeExerciseMedia(string id, [FromBody] ChangeExerciseMediaDto request, CancellationToken cancellationToken)
    {
        var result = await service.ChangeAsync(HttpContext.GetAuthenticatedAccountContext(), id.ToIdOrEmpty<LgymApi.Domain.Entities.Exercise>(), mapper.Map<ChangeExerciseMediaDto, ChangeExerciseMediaInput>(request), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaMutationResult, ExerciseMediaMutationDto>(result.Value));
    }

    [HttpPost("media/cleanup")]
    [ProducesResponseType(typeof(ExerciseMediaCleanupDto), 200)]
    public async Task<IActionResult> CleanupExerciseMedia(CancellationToken cancellationToken)
    {
        var result = await service.CleanupAsync(HttpContext.GetAuthenticatedAccountContext(), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaCleanupResult, ExerciseMediaCleanupDto>(result.Value));
    }
}
