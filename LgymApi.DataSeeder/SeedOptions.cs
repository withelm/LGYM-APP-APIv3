namespace LgymApi.DataSeeder;

public sealed class SeedOptions
{
    public bool DropDatabase { get; init; }
    public bool UseMigrations { get; init; }
    public bool SeedDemoData { get; init; }
}
