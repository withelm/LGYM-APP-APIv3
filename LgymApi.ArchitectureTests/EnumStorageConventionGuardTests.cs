using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class EnumStorageConventionGuardTests
{
    /// <summary>
    /// Ensures that every enum-type property on a mapped EF Core entity uses a string value converter.
    /// This enforces the project convention: all enums are stored as varchar (HasConversion&lt;string&gt;()).
    /// </summary>
    [Test]
    public void All_Enum_Properties_In_Entities_Should_Use_String_Conversion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=arch-enum-guard;Username=arch-enum-guard;Password=arch-enum-guard")
            .Options;

        using var ctx = new AppDbContext(options);
        var violations = new List<string>();

        foreach (var entityType in ctx.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

                if (!underlyingType.IsEnum)
                    continue;

                var converter = property.GetTypeMapping().Converter;

                if (converter == null)
                {
                    violations.Add($"{entityType.ClrType.Name}.{property.Name} ({underlyingType.Name}) — no value converter configured");
                    continue;
                }

                // The converter must convert to string (ProviderClrType == typeof(string))
                if (converter.ProviderClrType != typeof(string))
                {
                    if (underlyingType == typeof(LgymApi.Domain.ValueObjects.DaysOfWeekSet) && converter.ProviderClrType == typeof(int))
                    {
                        continue;
                    }

                    violations.Add($"{entityType.ClrType.Name}.{property.Name} ({underlyingType.Name}) — converter maps to {converter.ProviderClrType.Name}, expected string");
                }
            }
        }

        violations.Sort(StringComparer.Ordinal);

        Assert.That(
            violations,
            Is.Empty,
            "All enum properties in EF Core entities must use HasConversion<string>(). " +
            $"Found {violations.Count} violations:\n" +
            string.Join(Environment.NewLine, violations));
    }
}
