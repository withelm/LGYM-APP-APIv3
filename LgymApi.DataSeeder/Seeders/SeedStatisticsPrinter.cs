namespace LgymApi.DataSeeder.Seeders;

public static class SeedStatisticsPrinter
{
    public static void PrintSummary(SeedContext context)
    {
        Console.WriteLine("\nSeed summary:");
        Console.WriteLine($"  - Users: {CountUsers(context)}");
        Console.WriteLine($"  - Exercises: {context.Exercises.Count}");
        Console.WriteLine($"  - Exercise translations: {context.ExerciseTranslations.Count}");
        Console.WriteLine($"  - Addresses: {context.Addresses.Count}");
        Console.WriteLine($"  - Gyms: {context.Gyms.Count}");
        Console.WriteLine($"  - Plans: {context.Plans.Count}");
        Console.WriteLine($"  - Plan days: {context.PlanDays.Count}");
        Console.WriteLine($"  - Plan day exercises: {context.PlanDayExercises.Count}");
        Console.WriteLine($"  - Trainings: {context.Trainings.Count}");
        Console.WriteLine($"  - Exercise scores: {context.ExerciseScores.Count}");
        Console.WriteLine($"  - Training exercise scores: {context.TrainingExerciseScores.Count}");
        Console.WriteLine($"  - Measurements: {context.Measurements.Count}");
        Console.WriteLine($"  - Main records: {context.MainRecords.Count}");
        Console.WriteLine($"  - Elo entries: {context.EloRegistries.Count}");
        Console.WriteLine($"  - App configs: {context.AppConfigs.Count}");
        Console.WriteLine();
    }

    private static int CountUsers(SeedContext context)
    {
        var count = 0;
        if (context.AdminUser != null)
        {
            count++;
        }

        if (context.TesterUser != null)
        {
            count++;
        }

        count += context.DemoUsers.Count;
        return count;
    }
}
