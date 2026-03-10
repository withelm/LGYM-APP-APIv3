namespace LgymApi.Application.Features.Training.Elo;

public static class CoreEloCalculator
{
    public static int Calculate(double prevWeight, int prevReps, double currentWeight, int currentReps)
    {
        const int k = 32;

        double GetWeightedScore(double weight, int reps)
        {
            if (weight <= 15)
            {
                return weight * 0.3 + reps * 0.7;
            }

            if (weight <= 80)
            {
                return weight * 0.5 + reps * 0.5;
            }

            return weight * 0.7 + reps * 0.3;
        }

        var prevScore = GetWeightedScore(prevWeight, prevReps);
        var accScore = GetWeightedScore(currentWeight, currentReps);
        var toleranceThreshold = prevWeight > 80 ? 0.1 * prevScore : 0.05 * prevScore;

        var expectedScore = Math.Abs(accScore - prevScore) <= toleranceThreshold
            ? 0.5
            : prevScore / (prevScore + accScore);

        var actualScore = accScore >= prevScore ? 1 : 0;
        var scoreDifference = (actualScore - expectedScore) * (Math.Abs(accScore - prevScore) < toleranceThreshold ? 0.5 : 1);
        var points = k * scoreDifference;

        return (int)Math.Round(points);
    }
}
