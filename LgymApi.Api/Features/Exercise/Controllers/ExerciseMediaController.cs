using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Filters;
using LgymApi.Api.Middleware;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Exercise.Controllers;

[ApiController]
[Route("api/exercise/media")]
[Authorize(Policy = AuthConstants.Policies.ManageGlobalExercises)]
[TypeFilter(typeof(ExerciseMediaRequestFilter))]
[RequestSizeLimit(ExerciseMediaLimits.MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = ExerciseMediaLimits.MaxRequestBytes, ValueLengthLimit = 1024, ValueCountLimit = 1, MultipartHeadersLengthLimit = 8192)]
public sealed class ExerciseMediaController(IExerciseMediaService service, IMapper mapper) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExerciseMediaUploadResponseDto), 200)]
    [ProducesResponseType(typeof(ResponseMessageDto), 400)]
    [ProducesResponseType(typeof(ResponseMessageDto), 401)]
    [ProducesResponseType(typeof(ResponseMessageDto), 403)]
    [ProducesResponseType(typeof(ResponseMessageDto), 413)]
    [ProducesResponseType(typeof(ResponseMessageDto), 422)]
    [ProducesResponseType(typeof(ResponseMessageDto), 503)]
    public async Task<IActionResult> UploadExerciseMedia([FromForm] ExerciseMediaUploadDto request, CancellationToken cancellationToken)
    {
        var bytes = await ExerciseMediaRequestFilter.ReadSourceAsync(request.File, cancellationToken);
        var result = await service.UploadAsync(HttpContext.GetAuthenticatedAccountContext(), bytes, cancellationToken);
        return result.IsFailure ? result.ToActionResult() : Ok(mapper.Map<ExerciseMediaUploadResult, ExerciseMediaUploadResponseDto>(result.Value));
    }
}
