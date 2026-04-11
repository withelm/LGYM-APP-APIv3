using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Trainer.Controllers;

public sealed partial class TrainerRelationshipController : ControllerBase
{
    [HttpGet("trainees")]
    [ProducesResponseType(typeof(TrainerDashboardTraineesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardTrainees([FromQuery] TrainerDashboardTraineesRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetDashboardTraineesAsync(trainer!, new TrainerDashboardTraineeQuery
        {
            Search = request.Search,
            Status = request.Status,
            SortBy = request.SortBy,
            SortDirection = request.SortDirection,
            Page = request.Page,
            PageSize = request.PageSize
        }, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(new TrainerDashboardTraineesResponse
        {
            Page = result.Value.Page,
            PageSize = result.Value.PageSize,
            Total = result.Value.Total,
            Items = _mapper.MapList<TrainerDashboardTraineeResult, TrainerDashboardTraineeDto>(result.Value.Items)
        });
    }

    [HttpGet("trainees/{traineeId}/trainings/dates")]
    [ProducesResponseType(typeof(List<DateTime>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingDates([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<DateTime>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeTrainingDatesAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpPost("trainees/{traineeId}/trainings/by-date")]
    [ProducesResponseType(typeof(List<TrainingByDateDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingByDate([FromRoute] string traineeId, [FromBody] TrainingByDateRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeTrainingByDateAsync(trainer!, parsedTraineeId, request.CreatedAt, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<TrainingByDateDetails, TrainingByDateDetailsDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/exercise-scores/chart")]
    [ProducesResponseType(typeof(List<ExerciseScoresChartDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeExerciseScoresChartData([FromRoute] string traineeId, [FromBody] ExerciseScoresChartRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        if (!Id<ExerciseEntity>.TryParse(request.ExerciseId, out var parsedExerciseId))
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.ExerciseIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeExerciseScoresChartDataAsync(trainer!, parsedTraineeId, parsedExerciseId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ExerciseScoresChartData, ExerciseScoresChartDataDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/elo/chart")]
    [ProducesResponseType(typeof(List<EloRegistryBaseChartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeEloChart([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeEloChartAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<EloRegistryChartEntry, EloRegistryBaseChartDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/main-records/history")]
    [ProducesResponseType(typeof(List<MainRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeMainRecordsHistory([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<LgymApi.Domain.Entities.MainRecord>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeMainRecordsHistoryAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mappedRecords = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordResponseDto>(result.Value);
        return Ok(mappedRecords);
    }

    [HttpPost("trainees/{traineeId}/unlink")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTrainee([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.UnlinkTraineeAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
