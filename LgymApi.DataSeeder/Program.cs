using LgymApi.Application.Services;
using LgymApi.DataSeeder;
using LgymApi.DataSeeder.Seeders;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== LgymApi DataSeeder ===");

var configuration = DataSeederProgram.BuildConfiguration(AppContext.BaseDirectory);
var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;
Console.WriteLine("Reading configuration from LgymApi.Api/appsettings.json...");
Console.WriteLine($"Connection: {MaskConnectionString(connectionString)}");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Connection string 'ConnectionStrings:Postgres' is missing. Aborting.");
    return;
}

Console.WriteLine();

var dropDatabase = ConsolePrompt.Confirm("Drop existing database before seeding?", false);
var migrationChoice = ConsolePrompt.Choose(
    "Apply EF Core migrations or use EnsureCreated?",
    new[] { "Migrate", "EnsureCreated" },
    "Migrate");
var seedDemo = ConsolePrompt.Confirm("Seed demo data?", false);

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
services.AddScoped<ILegacyPasswordService, LegacyPasswordService>();
services.AddScoped<IEntitySeeder, UserSeeder>();
services.AddScoped<IEntitySeeder, EloRegistrySeeder>();
services.AddScoped<IEntitySeeder, ExerciseSeeder>();
services.AddScoped<IEntitySeeder, ExerciseTranslationSeeder>();
services.AddScoped<IEntitySeeder, AddressSeeder>();
services.AddScoped<IEntitySeeder, GymSeeder>();
services.AddScoped<IEntitySeeder, PlanSeeder>();
services.AddScoped<IEntitySeeder, PlanDaySeeder>();
services.AddScoped<IEntitySeeder, PlanDayExerciseSeeder>();
services.AddScoped<IEntitySeeder, TrainingSeeder>();
services.AddScoped<IEntitySeeder, ExerciseScoreSeeder>();
services.AddScoped<IEntitySeeder, TrainingExerciseScoreSeeder>();
services.AddScoped<IEntitySeeder, MeasurementSeeder>();
services.AddScoped<IEntitySeeder, MainRecordSeeder>();
services.AddScoped<IEntitySeeder, AppConfigSeeder>();
services.AddScoped<SeedOrchestrator>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var orchestrator = scope.ServiceProvider.GetRequiredService<SeedOrchestrator>();

var options = new SeedOptions
{
    DropDatabase = dropDatabase,
    UseMigrations = migrationChoice.Equals("Migrate", StringComparison.OrdinalIgnoreCase),
    SeedDemoData = seedDemo
};

var seedContext = new SeedContext();
await orchestrator.RunAsync(context, seedContext, options, CancellationToken.None);
Console.WriteLine("All done! Database is ready.");

static string MaskConnectionString(string connectionString)
{
    return DataSeederProgram.MaskConnectionString(connectionString);
}
