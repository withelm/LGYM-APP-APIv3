using Microsoft.Extensions.Configuration;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class DataSeederProgramTests
{
    [Test]
    public void MaskConnectionString_Should_Mask_Password_And_Keep_Other_Parts()
    {
        var masked = DataSeederProgram.MaskConnectionString("Host=localhost;Password=secret;Username=user");

        Assert.That(masked, Does.Contain("Host=localhost"));
        Assert.That(masked, Does.Contain("Username=user"));
        Assert.That(masked, Does.Contain("Password=***"));
        Assert.That(masked, Does.Not.Contain("secret"));
    }

    [Test]
    public void MaskConnectionString_Should_Return_Empty_For_Whitespace()
    {
        var masked = DataSeederProgram.MaskConnectionString(" ");

        Assert.That(masked, Is.EqualTo("<empty>"));
    }

    [Test]
    public void BuildConfiguration_Should_Load_Base_And_Optional_Appsettings()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            var config = DataSeederProgram.BuildConfiguration(repoRoot);

            Assert.That(config.GetConnectionString("Postgres"), Is.EqualTo("Host=localhost"));
            Assert.That(config["FeatureFlag"], Is.EqualTo("true"));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static string CreateTempRepo()
    {
        var root = Path.Combine(Path.GetTempPath(), "lgym-seeder-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "LgymApi.sln"), string.Empty);

        var apiRoot = Path.Combine(root, "LgymApi.Api");
        Directory.CreateDirectory(apiRoot);

        var baseSettings = "{" +
                           "\"ConnectionStrings\": { \"Postgres\": \"Host=localhost\" }" +
                           "}";
        var optionalSettings = "{" +
                               "\"FeatureFlag\": \"true\"" +
                               "}";

        File.WriteAllText(Path.Combine(apiRoot, "appsettings.json"), baseSettings);
        File.WriteAllText(Path.Combine(apiRoot, "appsettings.Development.json"), optionalSettings);

        return root;
    }
}
