using System.Xml.Linq;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class DataSeederToolingGuardTests
{
    [Test]
    public void DataSeeder_Project_Should_Reference_EfCore_Design_For_Tooling()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var projectFile = Path.Combine(repoRoot, "LgymApi.DataSeeder", "LgymApi.DataSeeder.csproj");

        Assert.That(File.Exists(projectFile), Is.True, $"Project file '{projectFile}' not found.");

        var document = XDocument.Load(projectFile);
        var hasDesignPackage = document
            .Descendants()
            .Any(node => node.Name.LocalName == "PackageReference"
                         && string.Equals(node.Attribute("Include")?.Value, "Microsoft.EntityFrameworkCore.Design", StringComparison.Ordinal));

        Assert.That(
            hasDesignPackage,
            Is.True,
            "LgymApi.DataSeeder.csproj must reference Microsoft.EntityFrameworkCore.Design so the startup-project EF tooling path stays available.");
    }
}
