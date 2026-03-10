using LgymApi.Application.Features.Training.Elo;
using LgymApi.Application.Exceptions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloCalculationStrategyTests
{
    [Test]
    public void AssistanceStrategy_LowerWeightThanPrevious_GivesPositiveElo()
    {
        var previous = new ExerciseScore { Weight = 40, Unit = WeightUnits.Kilograms, Reps = 8 };
        var current = new ExerciseScore { Weight = 30, Unit = WeightUnits.Kilograms, Reps = 8 };

        var standard = new StandardEloCalculationStrategy();
        var assistance = new AssistanceEloCalculationStrategy();

        var standardPoints = standard.Calculate(previous, current);
        var assistancePoints = assistance.Calculate(previous, current);

        Assert.That(standardPoints, Is.LessThan(0));
        Assert.That(assistancePoints, Is.GreaterThan(0));
    }

    [Test]
    public void Resolver_ReturnsRegisteredStrategy_ForEnumValue()
    {
        var resolver = new EloCalculationStrategyResolver(new IEloCalculationStrategy[]
        {
            new StandardEloCalculationStrategy(),
            new AssistanceEloCalculationStrategy()
        });

        var resolved = resolver.Resolve(EloStrategy.Assistance);

        Assert.That(resolved, Is.TypeOf<AssistanceEloCalculationStrategy>());
    }

    [Test]
    public void Resolver_Throws_WhenStrategyIsNotRegistered()
    {
        var resolver = new EloCalculationStrategyResolver(new IEloCalculationStrategy[]
        {
            new StandardEloCalculationStrategy()
        });

        Assert.Throws<AppException>(() => resolver.Resolve(EloStrategy.Assistance));
    }
}
