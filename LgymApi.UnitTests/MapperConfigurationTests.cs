using System.Text.Json;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Api;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.WorkoutProgress.Dashboard.Models;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MapperConfigurationTests
{
    [Test]
    public void Mapper_Should_Load_All_Profiles_And_Validate()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        Assert.That(mapper, Is.Not.Null);

        var concrete = mapper as Mapper;
        Assert.That(concrete, Is.Not.Null, "Mapper should be concrete implementation for validation");

        Assert.That(concrete!.RegisteredMappings.Count, Is.GreaterThan(0), "No mappings registered");
        var action = () => concrete.ValidateMappings();
        Assert.That(() => { (action).Invoke(); }, Throws.Nothing, "Mapping validation failed");
        Assert.That(mapper.Map<BodyParts, LgymApi.Application.Platform.ReferenceData.Enums.Models.EnumLookupEntry>(BodyParts.Chest)
            .Id, Is.EqualTo(nameof(BodyParts.Chest)));
    }

    [Test]
    public void Mapper_Should_Register_Each_Discovered_Profile_Exactly_Once()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var registeredProfiles = provider.GetServices<IMappingProfile>()
            .Select(profile => profile.GetType())
            .ToArray();
        var discoveredProfiles = LgymApi.Api.Mapping.MappingAssemblyMarkers.All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IMappingProfile).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToArray();

        Assert.That(registeredProfiles, Is.EquivalentTo(discoveredProfiles).UsingPropertiesComparer());
        Assert.That(registeredProfiles, Is.Unique);
    }

    [Test]
    public void MappingContext_Should_Reject_Unknown_Key()
    {
        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var context = mapper.CreateContext();

        var action = () => context.Set(new ContextKey<string>("Unknown.Key"), "value");
        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Duplicate_ContextKeys_Should_Throw()
    {
        var configuration = new MappingConfiguration();
        var key = new ContextKey<string>("Duplicate.Key");

        configuration.AllowContextKey(key);

        var action = () => configuration.AllowContextKey(key);
        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Map_Formula_String_To_Application_Enum()
    {
        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var dto = new ExerciseExtendedFormDto
        {
            Name = "Weighted pull-up",
            BodyPart = BodyParts.Back,
            EloFormula = new LookupItemVm { Id = ExerciseEloFormula.PullupWeighted.ToString(), DisplayName = "Pull-up weighted" },
            Description = "test",
            Image = "image"
        };

        var input = mapper.Map<ExerciseExtendedFormDto, AddExerciseWithFormulaInput>(dto);

        Assert.That(input.EloFormula, Is.EqualTo(ExerciseEloFormula.PullupWeighted));
    }

    [Test]
    public void ExerciseResponseDto_UsesLocalizedDisplayNameFromMappingContext()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var exerciseId = Id<Exercise>.New();
        var context = mapper.CreateContext();
        context.Set(
            new ContextKey<IReadOnlyDictionary<Id<Exercise>, string>>("Exercise.Translations"),
            new Dictionary<Id<Exercise>, string> { [exerciseId] = "Polska nazwa" });
        var exercise = new ProgressExerciseReadModel(
            exerciseId,
            "English name",
            null,
            BodyParts.Back,
            null,
            null,
            null);

        var dto = context.Map<ProgressExerciseReadModel, ExerciseResponseDto>(exercise);

        Assert.That(dto.DisplayName, Is.EqualTo("Polska nazwa"));
    }

    [Test]
    public void TrainerTrainingByDate_ComposesRegisteredExerciseMapsWithGlobalOnlyTranslations()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var globalExerciseId = Id<Exercise>.New();
        var customExerciseId = Id<Exercise>.New();
        var context = mapper.CreateContext();
        context.Set(
            new ContextKey<IReadOnlyDictionary<Id<Exercise>, string>>("Exercise.Translations"),
            new Dictionary<Id<Exercise>, string>
            {
                [globalExerciseId] = "Polska nazwa",
                [customExerciseId] = "Ignored translation"
            });
        var training = new WorkoutProgressDashboardTrainingReadModel(
            Id<Training>.New().ToString(),
            Id<LgymApi.TrainingPlanning.Contracts.PlanDayReference>.New().ToString(),
            DateTime.UtcNow,
            null,
            "Gym",
            [
                new WorkoutProgressDashboardExerciseReadModel(
                    Id<ExerciseScore>.New().ToString(),
                    new WorkoutProgressDashboardExerciseDetailsReadModel(globalExerciseId.ToString(), "English name", null, BodyParts.Back, null, null, null),
                    [new WorkoutProgressDashboardExerciseScoreReadModel(Id<ExerciseScore>.New().ToString(), globalExerciseId.ToString(), 80, WeightUnits.Kilograms, 8, 1)]),
                new WorkoutProgressDashboardExerciseReadModel(
                    Id<ExerciseScore>.New().ToString(),
                    new WorkoutProgressDashboardExerciseDetailsReadModel(customExerciseId.ToString(), "Custom name", Id<User>.New().ToString(), BodyParts.Back, null, null, null),
                    [])
            ]);

        var dto = context.Map<WorkoutProgressDashboardTrainingReadModel, LgymApi.Api.Features.Training.Contracts.TrainingByDateDetailsDto>(training);

        Assert.That(dto.Exercises[0].ExerciseDetails.Name, Is.EqualTo("English name"));
        Assert.That(dto.Exercises[0].ExerciseDetails.DisplayName, Is.EqualTo("Polska nazwa"));
        var assertedValue127 = (dto.Exercises[0].ScoresDetails).ToArray();
        Assert.That(assertedValue127, Has.Exactly(1).Items);
        Assert.That(assertedValue127.Single().Unit, Is.Not.Null);
        Assert.That(dto.Exercises[1].ExerciseDetails.DisplayName, Is.EqualTo("Custom name"));
    }

    [Test]
    public void PlanExerciseResponseDto_FallsBackToStoredNameForDisplayName()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var exercise = new LgymApi.Application.TrainingPlanning.Contracts.PlanDay.PlanExerciseReadModel(
            Id<LgymApi.TrainingPlanning.Contracts.PlanExerciseReference>.New(),
            "Bench Press",
            null,
            BodyParts.Chest,
            ExerciseEloFormula.Standard,
            null,
            null);

        var dto = mapper.Map<LgymApi.Application.TrainingPlanning.Contracts.PlanDay.PlanExerciseReadModel, ExerciseResponseDto>(exercise);

        Assert.That(dto.DisplayName, Is.EqualTo("Bench Press"));
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Map_User_Id_Through_Context()
    {
        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var context = mapper.CreateContext();
        var userId = Id<LgymApi.Identity.Contracts.AccountReference>.New();
        context.Set(new ContextKey<Id<LgymApi.Identity.Contracts.AccountReference>>("Exercise.UserId"), userId);

        var dto = new ExerciseExtendedFormDto
        {
            Name = "Weighted pull-up",
            BodyPart = BodyParts.Back,
            EloFormula = new LookupItemVm { Id = ExerciseEloFormula.PullupWeighted.ToString(), DisplayName = "Pull-up weighted" },
            Description = "test",
            Image = "image"
        };

        var input = context.Map<ExerciseExtendedFormDto, AddUserExerciseWithFormulaInput>(dto);

        Assert.That(input.UserId, Is.EqualTo(userId));
        Assert.That(input.EloFormula, Is.EqualTo(ExerciseEloFormula.PullupWeighted));
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Require_BodyPart_When_Deserialized()
    {
        const string json = """
            {
              "name": "Weighted pull-up",
              "eloFormula": { "id": "PullupWeighted", "displayName": "Pull-up weighted" }
            }
            """;

        var action = () => JsonSerializer.Deserialize<ExerciseExtendedFormDto>(json);

        Assert.That(() => { action(); }, Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Deserialize_Legacy_String_Formula()
    {
        const string json = """
            {
              "name": "Weighted pull-up",
              "bodyPart": 4,
              "eloFormula": "PullupWeighted"
            }
            """;

        var dto = JsonSerializer.Deserialize<ExerciseExtendedFormDto>(json);

        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.EloFormula, Is.Not.Null);
        Assert.That(dto.EloFormula!.Id, Is.EqualTo(ExerciseEloFormula.PullupWeighted.ToString()));

        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var input = mapper.Map<ExerciseExtendedFormDto, AddExerciseWithFormulaInput>(dto);

        Assert.That(input.EloFormula, Is.EqualTo(ExerciseEloFormula.PullupWeighted));
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Deserialize_Lookup_Object_Name_As_DisplayName_Fallback()
    {
        const string json = """
            {
              "name": "Weighted pull-up",
              "bodyPart": 4,
              "eloFormula": { "id": "PullupWeighted", "name": "Pull-up weighted" }
            }
            """;

        var dto = JsonSerializer.Deserialize<ExerciseExtendedFormDto>(json);

        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.EloFormula, Is.Not.Null);
        Assert.That(dto.EloFormula!.Id, Is.EqualTo(ExerciseEloFormula.PullupWeighted.ToString()));
        Assert.That(dto.EloFormula.DisplayName, Is.EqualTo("Pull-up weighted"));

        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var input = mapper.Map<ExerciseExtendedFormDto, AddExerciseWithFormulaInput>(dto);

        Assert.That(input.EloFormula, Is.EqualTo(ExerciseEloFormula.PullupWeighted));
    }

    [Test]
    public void Chart_Profiles_Should_Map_Typed_Entity_Ids_To_Uuid_Strings()
    {
        var exerciseId = Id<Exercise>.New();
        var eloRegistryId = Id<EloRegistry>.New();
        var exerciseScores = new ExerciseScoresChartData
        {
            Id = "presentation-key",
            Value = 120,
            Date = "01/02",
            ExerciseName = "Squat"
        };
        var eloRegistry = new EloRegistryChartEntry
        {
            Value = 1020,
            Date = "01/03"
        };
        var exerciseIdProperty = typeof(ExerciseScoresChartData).GetProperty(nameof(ExerciseScoresChartData.ExerciseId));
        var eloRegistryIdProperty = typeof(EloRegistryChartEntry).GetProperty(nameof(EloRegistryChartEntry.Id));

        Assert.That(exerciseIdProperty!.PropertyType, Is.EqualTo(typeof(Id<Exercise>)));
        Assert.That(eloRegistryIdProperty!.PropertyType, Is.EqualTo(typeof(Id<EloRegistry>)));
        exerciseIdProperty.SetValue(exerciseScores, exerciseId);
        eloRegistryIdProperty.SetValue(eloRegistry, eloRegistryId);

        var services = new ServiceCollection();
services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var exerciseScoresDto = mapper.Map<ExerciseScoresChartData, ExerciseScoresChartDataDto>(exerciseScores);
        var eloRegistryDto = mapper.Map<EloRegistryChartEntry, EloRegistryBaseChartDto>(eloRegistry);

        Assert.That(exerciseScoresDto.Id, Is.EqualTo("presentation-key"));
        Assert.That(exerciseScoresDto.ExerciseId, Is.EqualTo(exerciseId.ToString()));
        Assert.That(eloRegistryDto.Id, Is.EqualTo(eloRegistryId.ToString()));
    }
}
