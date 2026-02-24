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

        Assert.Multiple(() =>
        {
            Assert.That(options.DropDatabase, Is.True);
            Assert.That(options.UseMigrations, Is.False);
            Assert.That(options.SeedDemoData, Is.True);
        });
    }
}
