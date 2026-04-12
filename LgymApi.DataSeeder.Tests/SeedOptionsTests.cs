namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeedOptionsTests
{
    [Test]
    public void SeedOptions_Should_Preserve_Assigned_Values()
    {
        var options = new SeedOptions
        {
            DropDatabase = true,
            UseMigrations = false,
            SeedDemoData = true
        };

        options.DropDatabase.Should().BeTrue();
        options.UseMigrations.Should().BeFalse();
        options.SeedDemoData.Should().BeTrue();
    }
}
