using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Features.Exercise.Models;
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
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        mapper.Should().NotBeNull();

        var concrete = mapper as Mapper;
        concrete.Should().NotBeNull("Mapper should be concrete implementation for validation");

        concrete!.RegisteredMappings.Count.Should().BeGreaterThan(0, "No mappings registered");
        var action = () => concrete.ValidateMappings();
        action.Should().NotThrow("Mapping validation failed");
    }

    [Test]
    public void MappingContext_Should_Reject_Unknown_Key()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var context = mapper.CreateContext();

        var action = () => context.Set(new ContextKey<string>("Unknown.Key"), "value");
        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Duplicate_ContextKeys_Should_Throw()
    {
        var configuration = new MappingConfiguration();
        var key = new ContextKey<string>("Duplicate.Key");

        configuration.AllowContextKey(key);

        var action = () => configuration.AllowContextKey(key);
        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Map_Formula_String_To_Application_Enum()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

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

        input.EloFormula.Should().Be(ExerciseEloFormula.PullupWeighted);
    }

    [Test]
    public void ExerciseExtendedFormDto_Should_Map_User_Id_Through_Context()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var context = mapper.CreateContext();
        var userId = Id<LgymApi.Domain.Entities.User>.New();
        context.Set(new ContextKey<Id<LgymApi.Domain.Entities.User>>("Exercise.UserId"), userId);

        var dto = new ExerciseExtendedFormDto
        {
            Name = "Weighted pull-up",
            BodyPart = BodyParts.Back,
            EloFormula = new LookupItemVm { Id = ExerciseEloFormula.PullupWeighted.ToString(), DisplayName = "Pull-up weighted" },
            Description = "test",
            Image = "image"
        };

        var input = context.Map<ExerciseExtendedFormDto, AddUserExerciseWithFormulaInput>(dto);

        input.UserId.Should().Be(userId);
        input.EloFormula.Should().Be(ExerciseEloFormula.PullupWeighted);
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

        action.Should().Throw<JsonException>();
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

        dto.Should().NotBeNull();
        dto!.EloFormula.Should().NotBeNull();
        dto.EloFormula!.Id.Should().Be(ExerciseEloFormula.PullupWeighted.ToString());

        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var input = mapper.Map<ExerciseExtendedFormDto, AddExerciseWithFormulaInput>(dto);

        input.EloFormula.Should().Be(ExerciseEloFormula.PullupWeighted);
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

        dto.Should().NotBeNull();
        dto!.EloFormula.Should().NotBeNull();
        dto.EloFormula!.Id.Should().Be(ExerciseEloFormula.PullupWeighted.ToString());
        dto.EloFormula.DisplayName.Should().Be("Pull-up weighted");

        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var input = mapper.Map<ExerciseExtendedFormDto, AddExerciseWithFormulaInput>(dto);

        input.EloFormula.Should().Be(ExerciseEloFormula.PullupWeighted);
    }
}
