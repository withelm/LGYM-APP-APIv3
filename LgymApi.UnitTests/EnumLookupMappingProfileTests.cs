using System.Globalization;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.ReferenceData.Enums;
using LgymApi.Application.Platform.ReferenceData.Enums.Models;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class EnumLookupMappingProfileTests
{
    [Test]
    public void Mapper_Should_Register_Exactly_The_Supported_Enum_Lookup_Maps()
    {
        var mapper = CreateApplicationMapper();
        var assertedValue109 = mapper;
        Assert.That(assertedValue109, Is.TypeOf<Mapper>());
        var concreteMapper = ((Mapper)assertedValue109);

        var enumLookupMaps = concreteMapper.RegisteredMappings
            .Where(map => map.Target == typeof(EnumLookupEntry))
            .Select(map => map.Source)
            .ToList();

        Assert.That(enumLookupMaps, Is.EquivalentTo(new[] { typeof(BodyParts), typeof(ExerciseEloFormula), typeof(MeasurementUnits), typeof(WeightUnits), typeof(HeightUnits), typeof(Platforms), typeof(PlanPurpose) }));
    }

    [Test]
    public void Mapper_Should_Reject_A_Lookup_When_Its_Concrete_Enum_Map_Is_Omitted()
    {
        var mapper = new Mapper([new MissingEnumLookupMappingProfile()]);

        var action = () => mapper.Map<EnumLookupEntry>(BodyParts.Chest);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Mapping from BodyParts to EnumLookupEntry is not registered.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Mapper_Should_Prefer_The_Explicit_Culture_Context_Over_Current_Ui_Culture()
    {
        var mapper = CreateApplicationMapper();
        using var cultureScope = new CurrentUiCultureScope("pl");
        var context = mapper.CreateContext();
        context.Set(EnumLookupContextKeys.Culture, CultureInfo.GetCultureInfo("en"));

        var lookup = mapper.Map<ExerciseEloFormula, EnumLookupEntry>(ExerciseEloFormula.PullupWeighted, context);

        Assert.That(lookup.Name, Is.EqualTo("Pull-up weighted"));
        Assert.That(lookup.DisplayName, Is.EqualTo("Pull-up weighted"));
    }

    [Test]
    public void Mapper_Should_Use_Current_Ui_Culture_When_The_Culture_Context_Is_Omitted()
    {
        var mapper = CreateApplicationMapper();
        using var cultureScope = new CurrentUiCultureScope("pl");

        var lookup = mapper.Map<ExerciseEloFormula, EnumLookupEntry>(ExerciseEloFormula.PullupWeighted);

        Assert.That(lookup.Name, Is.EqualTo("Podciąganie: im mniejszy ciężar, tym lepiej"));
        Assert.That(lookup.DisplayName, Is.EqualTo("Podciąganie: im mniejszy ciężar, tym lepiej"));
    }

    [Test]
    public void EnumService_Should_Keep_Hidden_Values_Out_Of_Lookups()
    {
        var service = new EnumService(CreateApplicationMapper());

        var values = service.GetLookup<WeightUnits>(CultureInfo.GetCultureInfo("en"));

        Assert.That(values.Select(value => value.Id), Does.Not.Contain(nameof(WeightUnits.Unknown)));
    }

    [TestCase("en", "Pull-up weighted")]
    [TestCase("pl", "Podciąganie: im mniejszy ciężar, tym lepiej")]
    public void EnumService_Should_Map_All_Supported_Enums_And_Exclude_Hidden_Values(string cultureName, string expectedPullupLabel)
    {
        var service = new EnumService(CreateApplicationMapper());
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var lookups = new List<List<EnumLookupEntry>>
        {
            service.GetLookup<BodyParts>(culture),
            service.GetLookup<ExerciseEloFormula>(culture),
            service.GetLookup<MeasurementUnits>(culture),
            service.GetLookup<WeightUnits>(culture),
            service.GetLookup<HeightUnits>(culture),
            service.GetLookup<Platforms>(culture),
            service.GetLookup<PlanPurpose>(culture)
        };
        var pullupWeighted = lookups[1].Single(value => value.Id == nameof(ExerciseEloFormula.PullupWeighted));

        Assert.Multiple(() =>
        {
            foreach (var values in lookups)
            {
                Assert.That(values, Is.Not.Empty);
                Assert.That(values.Select(value => value.Id), Does.Not.Contain("Unknown"));
            }
        });
        Assert.That(pullupWeighted.Name, Is.EqualTo(expectedPullupLabel));
        Assert.That(pullupWeighted.DisplayName, Is.EqualTo(expectedPullupLabel));
    }

    private static IMapper CreateApplicationMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }

    private sealed class MissingEnumLookupMappingProfile : IMappingProfile
    {
        public void Configure(MappingConfiguration configuration)
        {
        }
    }

    private sealed class CurrentUiCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentUICulture;

        public CurrentUiCultureScope(string cultureName)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _previousCulture;
        }
    }
}
