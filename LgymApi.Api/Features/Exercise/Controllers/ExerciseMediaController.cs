using LgymApi.Api.Middleware;
using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Filters;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Exercise.Controllers;

[ApiController]
[Route("api/exercise")]
[Authorize(Policy = AuthConstants.Policies.ManageGlobalExercises)]
[TypeFilter(typeof(ExerciseMediaRequestFilter))]
[RequestSizeLimit(ExerciseMediaLimits.MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = ExerciseMediaLimits.MaxRequestBytes, ValueLengthLimit = 64 * 1024, ValueCountLimit = 32)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 400)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 401)]
[ProducesResponseType(typeof(LgymApi.Api.Features.Common.Contracts.ResponseMessageDto), 403)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 404)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 409)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 413)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 422)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 428)]
[ProducesResponseType(typeof(ExerciseMediaErrorDto), 503)]
public sealed class ExerciseMediaController(IExerciseMediaService service, IMapper mapper) : ControllerBase
{
    [HttpPost("media/create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExerciseMediaMutationDto), 200)]
    public async Task<IActionResult> CreateExerciseWithMedia([FromForm] CreateExerciseMediaDto request, CancellationToken cancellationToken)
    {
        var bytes = await ExerciseMediaRequestFilter.ReadSourceAsync(request.File, cancellationToken);
        var result = await service.CreateAsync(HttpContext.GetAuthenticatedAccountContext(), mapper.Map<CreateExerciseMediaDto, CreateExerciseMediaInput>(request), bytes, cancellationToken);
        return MutationResult(result, request.OperationId);
    }

    [HttpPatch("{id}/media")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExerciseMediaMutationDto), 200)]
    public async Task<IActionResult> ChangeExerciseMedia(string id, [FromForm] ChangeExerciseMediaDto request, CancellationToken cancellationToken)
    {
        var bytes = await ExerciseMediaRequestFilter.ReadSourceAsync(request.File, cancellationToken);
        var result = await service.ChangeAsync(HttpContext.GetAuthenticatedAccountContext(), id.ToIdOrEmpty<LgymApi.Domain.Entities.Exercise>(), mapper.Map<ChangeExerciseMediaDto, ChangeExerciseMediaInput>(request), bytes, cancellationToken);
        return MutationResult(result, request.OperationId);
    }

    [HttpPost("media/cleanup")]
    [ProducesResponseType(typeof(ExerciseMediaCleanupDto), 200)]
    public async Task<IActionResult> CleanupExerciseMedia(CancellationToken cancellationToken)
    {
        var result = await service.CleanupAsync(HttpContext.GetAuthenticatedAccountContext(), cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaCleanupResult, ExerciseMediaCleanupDto>(result.Value));
    }

    private IActionResult MutationResult(Result<ExerciseMediaMutationResult, AppError> result, string operationId)
    {
        if (result.IsSuccess) return Ok(mapper.Map<ExerciseMediaMutationResult, ExerciseMediaMutationDto>(result.Value));
        var disposition = result.Error is ExerciseMediaOperationError error ? error.Disposition : ExerciseMediaOperationError.Unresolved;
        return StatusCode(result.Error.HttpStatusCode, mapper.Map<ExerciseMediaFailureResult, ExerciseMediaErrorDto>(new(operationId, result.Error.Message, disposition)));
    }
}
