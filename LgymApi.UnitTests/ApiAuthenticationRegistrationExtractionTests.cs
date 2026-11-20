
namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApiAuthenticationRegistrationExtractionTests
{
    [Test]
    public void Program_DelegatesAuthenticationRegistrationToApiOnlyExtension()
    {
        var repositoryRoot = FindRepositoryRoot();
        var programSource = File.ReadAllText(Path.Combine(repositoryRoot, "LgymApi.Api", "Program.cs"));
        var extensionPath = Path.Combine(repositoryRoot, "LgymApi.Api", "Configuration", "ApiAuthenticationExtensions.cs");

        Assert.That(File.Exists(extensionPath), Is.True);
        Assert.That(programSource, Does.Contain("builder.Services.AddApiAuthentication(builder.Configuration);"));
        Assert.That(programSource, Does.Not.Contain("AddAuthentication(JwtBearerDefaults.AuthenticationScheme)"));
        Assert.That(programSource, Does.Not.Contain("AddJwtBearer("));
        Assert.That(programSource, Does.Not.Contain("new TokenValidationParameters"));
        Assert.That(programSource, Does.Not.Contain("new JwtBearerEvents"));

        var extensionSource = File.ReadAllText(extensionPath);
        Assert.That(extensionSource, Does.Contain("namespace LgymApi.Api.Configuration;"));
        Assert.That(extensionSource, Does.Contain("AddAuthentication(JwtBearerDefaults.AuthenticationScheme)"));
        Assert.That(extensionSource, Does.Contain("AddJwtBearer("));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root containing LgymApi.slnx.");
    }
}
