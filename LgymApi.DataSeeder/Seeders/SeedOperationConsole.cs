namespace LgymApi.DataSeeder.Seeders;

public static class SeedOperationConsole
{
    public static void Start(string label)
    {
        Console.WriteLine($"Seeding {label}...");
    }

    public static void Done(string label)
    {
        Console.WriteLine($"Seeding {label}... Done.");
    }

    public static void Skip(string label)
    {
        Console.WriteLine($"Seeding {label}... Skipped.");
    }
}
