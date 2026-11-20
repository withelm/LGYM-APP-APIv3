using LgymApi.Application.WorkoutProgress.Scoring.Elo;
using LgymApi.Domain.Enums;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseEloCalculatorTests
{
    [Test]
    public void Calculators_Should_Preserve_Formula_Selection_And_Golden_Gains()
    {
        var input = new ExerciseEloCalculationInput(
            PreviousWeight: 80,
            PreviousReps: 5,
            CurrentWeight: 80,
            CurrentReps: 10);

        Assert.Multiple(() =>
        {
            Assert.That(new StandardExerciseEloCalculator().Formula, Is.EqualTo(ExerciseEloFormula.Standard));
            Assert.That(new StandardExerciseEloCalculator().Calculate(input), Is.EqualTo(16));
            Assert.That(new StrengthWeightedExerciseEloCalculator().Formula, Is.EqualTo(ExerciseEloFormula.StrengthWeighted));
            Assert.That(new StrengthWeightedExerciseEloCalculator().Calculate(input), Is.EqualTo(8));
            Assert.That(new VolumeWeightedExerciseEloCalculator().Formula, Is.EqualTo(ExerciseEloFormula.VolumeWeighted));
            Assert.That(new VolumeWeightedExerciseEloCalculator().Calculate(input), Is.EqualTo(17));
        });
    }

    [Test]
    public void PullupWeightedCalculator_Should_Preserve_Golden_Gains()
    {
        var calculator = new PullupWeightedExerciseEloCalculator();

        Assert.Multiple(() =>
        {
            Assert.That(calculator.Formula, Is.EqualTo(ExerciseEloFormula.PullupWeighted));
            Assert.That(calculator.Calculate(new ExerciseEloCalculationInput(80, 8, 60, 8)), Is.EqualTo(17));
            Assert.That(calculator.Calculate(new ExerciseEloCalculationInput(80, 8, 100, 8)), Is.EqualTo(-17));
        });
    }
}
