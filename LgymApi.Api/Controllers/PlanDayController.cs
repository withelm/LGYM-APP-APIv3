using LgymApi.Api.DTOs;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class PlanDayController : ControllerBase
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IPlanDayExerciseRepository _planDayExerciseRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITrainingRepository _trainingRepository;

    public PlanDayController(
        IPlanRepository planRepository,
        IPlanDayRepository planDayRepository,
        IPlanDayExerciseRepository planDayExerciseRepository,
        IExerciseRepository exerciseRepository,
        IUserRepository userRepository,
        ITrainingRepository trainingRepository)
    {
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
        _planDayExerciseRepository = planDayExerciseRepository;
        _exerciseRepository = exerciseRepository;
        _userRepository = userRepository;
        _trainingRepository = trainingRepository;
    }

    [HttpPost("planDay/{id}/createPlanDay")]
    public async Task<IActionResult> CreatePlanDay([FromRoute] string id, [FromBody] PlanDayFormDto form)
    {
        if (!Guid.TryParse(id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        if (string.IsNullOrWhiteSpace(form.Name) || form.Exercises == null || form.Exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Message.FieldRequired });
        }

        var planDay = new PlanDay
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Name = form.Name,
            IsDeleted = false
        };

        await _planDayRepository.AddAsync(planDay);

        var exercisesToAdd = new List<PlanDayExercise>();
        foreach (var exercise in form.Exercises)
        {
            if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
            {
                continue;
            }

            exercisesToAdd.Add(new PlanDayExercise
            {
                Id = Guid.NewGuid(),
                PlanDayId = planDay.Id,
                ExerciseId = exerciseId,
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd);
        }
        return Ok(new ResponseMessageDto { Message = Message.Created });
    }

    [HttpPost("planDay/updatePlanDay")]
    public async Task<IActionResult> UpdatePlanDay([FromBody] PlanDayFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || form.Exercises == null || form.Exercises.Count == 0)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Message.FieldRequired });
        }

        if (!Guid.TryParse(form.Id, out var planDayId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId);
        if (planDay == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        planDay.Name = form.Name;
        await _planDayRepository.UpdateAsync(planDay);

        await _planDayExerciseRepository.RemoveByPlanDayIdAsync(planDay.Id);

        var exercisesToAdd = new List<PlanDayExercise>();
        foreach (var exercise in form.Exercises)
        {
            if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
            {
                continue;
            }

            exercisesToAdd.Add(new PlanDayExercise
            {
                Id = Guid.NewGuid(),
                PlanDayId = planDay.Id,
                ExerciseId = exerciseId,
                Series = exercise.Series,
                Reps = exercise.Reps
            });
        }

        if (exercisesToAdd.Count > 0)
        {
            await _planDayExerciseRepository.AddRangeAsync(exercisesToAdd);
        }
        return Ok(new ResponseMessageDto { Message = Message.Updated });
    }

    [HttpGet("planDay/{id}/getPlanDay")]
    public async Task<IActionResult> GetPlanDay([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var planDayId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId);
        if (planDay == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var exercises = await _planDayExerciseRepository.GetByPlanDayIdAsync(planDay.Id);
        var exerciseIds = exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        var planDayVm = new PlanDayVmDto
        {
            Id = planDay.Id.ToString(),
            Name = planDay.Name,
            Exercises = exercises.Select(e => new PlanDayExerciseVmDto
            {
                Series = e.Series,
                Reps = e.Reps,
                Exercise = exerciseMap.TryGetValue(e.ExerciseId, out var exercise)
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
            }).ToList()
        };

        return Ok(planDayVm);
    }

    [HttpGet("planDay/{id}/getPlanDays")]
    public async Task<IActionResult> GetPlanDays([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id);

        if (planDays.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds);

        var exerciseIds = planDayExercises.Select(e => e.ExerciseId).Distinct().ToList();
        var exerciseList = await _exerciseRepository.GetByIdsAsync(exerciseIds);
        var exerciseMap = exerciseList.ToDictionary(e => e.Id, e => e);

        var result = planDays.Select(planDay =>
        {
            var exercises = planDayExercises.Where(e => e.PlanDayId == planDay.Id).ToList();
            var vmExercises = exercises.Select(e => new PlanDayExerciseVmDto
            {
                Series = e.Series,
                Reps = e.Reps,
                Exercise = exerciseMap.TryGetValue(e.ExerciseId, out var ex)
                    ? new ExerciseFormDto
                    {
                        Id = ex.Id.ToString(),
                        Name = ex.Name,
                        BodyPart = ex.BodyPart.ToString(),
                        Description = ex.Description,
                        Image = ex.Image,
                        UserId = ex.UserId?.ToString()
                    }
                    : new ExerciseFormDto()
            }).ToList();

            return new PlanDayVmDto
            {
                Id = planDay.Id.ToString(),
                Name = planDay.Name,
                Exercises = vmExercises
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("planDay/{id}/getPlanDaysTypes")]
    public async Task<IActionResult> GetPlanDaysTypes([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var plan = await _planRepository.FindActiveByUserIdAsync(user.Id);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id);
        var planDayDtos = planDays.Select(pd => new PlanDayChooseDto
        {
            Id = pd.Id.ToString(),
            Name = pd.Name
        }).ToList();

        return Ok(planDayDtos);
    }

    [HttpGet("planDay/{id}/deletePlanDay")]
    public async Task<IActionResult> DeletePlanDay([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var planDayId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDay = await _planDayRepository.FindByIdAsync(planDayId);
        if (planDay == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        await _planDayRepository.MarkDeletedAsync(planDay.Id);
        return Ok(new ResponseMessageDto { Message = Message.Deleted });
    }

    [HttpGet("planDay/{id}/getPlanDaysInfo")]
    public async Task<IActionResult> GetPlanDaysInfo([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var planId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDays = await _planDayRepository.GetByPlanIdAsync(plan.Id);

        if (planDays.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Message.DidntFind });
        }

        var planDayIds = planDays.Select(pd => pd.Id).ToList();
        var planDayExercises = await _planDayExerciseRepository.GetByPlanDayIdsAsync(planDayIds);

        var trainings = await _trainingRepository.GetByPlanDayIdsAsync(planDayIds);
        var lastTrainingMap = trainings
            .GroupBy(t => t.TypePlanDayId)
            .ToDictionary(g => g.Key, g => (DateTime?)g.Max(t => t.CreatedAt).UtcDateTime);

        var result = planDays.Select(planDay =>
        {
            var exercises = planDayExercises.Where(e => e.PlanDayId == planDay.Id).ToList();
            return new PlanDayBaseInfoDto
            {
                Id = planDay.Id.ToString(),
                Name = planDay.Name,
                LastTrainingDate = lastTrainingMap.TryGetValue(planDay.Id, out var lastDate)
                    ? lastDate
                    : null,
                TotalNumberOfSeries = exercises.Sum(e => e.Series),
                TotalNumberOfExercises = exercises.Count
            };
        }).ToList();

        return Ok(result);
    }
}
