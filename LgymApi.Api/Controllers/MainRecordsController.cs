using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class MainRecordsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IMainRecordRepository _mainRecordRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;

    public MainRecordsController(
        IUserRepository userRepository,
        IExerciseRepository exerciseRepository,
        IMainRecordRepository mainRecordRepository,
        IExerciseScoreRepository exerciseScoreRepository)
    {
        _userRepository = userRepository;
        _exerciseRepository = exerciseRepository;
        _mainRecordRepository = mainRecordRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
    }

    [HttpPost("mainRecords/{id}/addNewRecord")]
    public async Task<IActionResult> AddNewRecord([FromRoute] string id, [FromBody] MainRecordsFormDto form)
    {
        if (!Guid.TryParse(id, out var userId) || !Guid.TryParse(form.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var unit = form.Unit == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;
        var record = new MainRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExerciseId = exercise.Id,
            Weight = form.Weight,
            Unit = unit,
            Date = new DateTimeOffset(DateTime.SpecifyKind(form.Date, DateTimeKind.Utc))
        };

        await _mainRecordRepository.AddAsync(record);
        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpGet("mainRecords/{id}/getMainRecordsHistory")]
    public async Task<IActionResult> GetMainRecordsHistory([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id);
        records = records.OrderByDescending(r => r.Date).ToList();

        if (records.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = records.Reverse<MainRecord>().Select(record => new MainRecordsFormDto
        {
            Id = record.Id.ToString(),
            ExerciseId = record.ExerciseId.ToString(),
            Weight = record.Weight,
            Unit = record.Unit == WeightUnits.Kilograms ? "kg" : "lbs",
            Date = record.Date.UtcDateTime
        }).ToList();

        return Ok(result);
    }

    [HttpGet("mainRecords/{id}/getLastMainRecords")]
    public async Task<IActionResult> GetLastMainRecords([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var records = await _mainRecordRepository.GetByUserIdAsync(user.Id);

        if (records.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var latestRecords = records
            .GroupBy(r => r.ExerciseId)
            .Select(g => g.OrderByDescending(r => r.Date).First())
            .ToList();

        var exerciseIds = latestRecords.Select(r => r.ExerciseId).Distinct().ToList();
        var exercises = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exercises.ToDictionary(e => e.Id, e => e);

        var result = latestRecords.Select(record => new MainRecordsLastDto
        {
            Id = record.Id.ToString(),
            ExerciseId = record.ExerciseId.ToString(),
            Weight = record.Weight,
            Unit = record.Unit == WeightUnits.Kilograms ? "kg" : "lbs",
            Date = record.Date.UtcDateTime,
            ExerciseDetails = exerciseMap.TryGetValue(record.ExerciseId, out var exercise)
                ? new ExerciseFormDto
                {
                    Id = exercise.Id.ToString(),
                    Name = exercise.Name,
                    BodyPart = exercise.BodyPart.ToString(),
                    Description = exercise.Description,
                    Image = exercise.Image,
                    UserId = exercise.UserId?.ToString()
                }
                : new ExerciseFormDto()
        }).ToList();

        return Ok(result);
    }

    [HttpGet("mainRecords/{id}/deleteMainRecord")]
    public async Task<IActionResult> DeleteMainRecord([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var recordId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var record = await _mainRecordRepository.FindByIdAsync(recordId);
        if (record == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        await _mainRecordRepository.DeleteAsync(record);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }

    [HttpPost("mainRecords/{id}/updateMainRecords")]
    public async Task<IActionResult> UpdateMainRecords([FromRoute] string id, [FromBody] MainRecordsFormDto form)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(form.Id, out var recordId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var existingRecord = await _mainRecordRepository.FindByIdAsync(recordId);
        if (existingRecord == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        if (!Guid.TryParse(form.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var exercise = await _exerciseRepository.FindByIdAsync(exerciseId);
        if (exercise == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        existingRecord.UserId = user.Id;
        existingRecord.ExerciseId = exercise.Id;
        existingRecord.Weight = form.Weight;
        existingRecord.Unit = form.Unit == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;
        existingRecord.Date = new DateTimeOffset(DateTime.SpecifyKind(form.Date, DateTimeKind.Utc));

        await _mainRecordRepository.UpdateAsync(existingRecord);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("mainRecords/getRecordOrPossibleRecordInExercise")]
    public async Task<IActionResult> GetRecordOrPossibleRecordInExercise([FromBody] RecordOrPossibleRequestDto request)
    {
        var userId = HttpContext.GetCurrentUser()?.Id;
        if (!userId.HasValue || !Guid.TryParse(request.ExerciseId, out var exerciseId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var record = await _mainRecordRepository.GetLatestByUserAndExerciseAsync(userId.Value, exerciseId);

        if (record == null)
        {
            var possible = await _exerciseScoreRepository.GetBestScoreAsync(userId.Value, exerciseId);

            if (possible == null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
            }

            return Ok(new PossibleRecordForExerciseDto
            {
                Weight = possible.Weight,
                Reps = possible.Reps,
                Unit = possible.Unit == WeightUnits.Kilograms ? "kg" : "lbs",
                Date = possible.CreatedAt.UtcDateTime
            });
        }

        return Ok(new PossibleRecordForExerciseDto
        {
            Weight = record.Weight,
            Reps = 1,
            Unit = record.Unit == WeightUnits.Kilograms ? "kg" : "lbs",
            Date = record.Date.UtcDateTime
        });
    }
}
