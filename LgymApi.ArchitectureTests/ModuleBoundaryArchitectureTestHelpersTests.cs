using Microsoft.CodeAnalysis.CSharp;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ModuleBoundaryArchitectureTestHelpersTests
{
    private static readonly object[] NonProductionPathCases =
    {
        new object[]
        {
            Path.Combine("LgymApi.Api", "bin", "Debug", "net10.0", "Generated.cs"),
            ModuleBoundaryExclusionKind.BuildArtifact
        },
        new object[]
        {
            Path.Combine("LgymApi.UnitTests", "Users", "UsersServiceTests.cs"),
            ModuleBoundaryExclusionKind.TestProject
        },
        new object[]
        {
            Path.Combine("LgymApi.Application", "Users", "Helpers", "UsersModuleHelper.cs"),
            ModuleBoundaryExclusionKind.Helper
        },
        new object[]
        {
            Path.Combine("LgymApi.Infrastructure", "Users", "Migrations", "202607140001_Init.cs"),
            ModuleBoundaryExclusionKind.GeneratedCode
        }
    };

    [Test]
    public void ClassifyModuleBoundaryFile_Recognizes_Api_Feature_Production_File()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var filePath = Path.Combine(repoRoot, "LgymApi.Api", "Features", "User", "Controllers", "UserController.cs");

        var classification = ArchitectureTestHelpers.ClassifyModuleBoundaryFile(filePath, repoRoot);

        Assert.Multiple(() =>
        {
            Assert.That(classification.IsProductionCode, Is.True);
            Assert.That(classification.ExclusionKind, Is.Null);
            Assert.That(classification.ModuleName, Is.EqualTo("User"));
            Assert.That(classification.RelativePath, Is.EqualTo("LgymApi.Api/Features/User/Controllers/UserController.cs"));
        });
    }

    [TestCaseSource(nameof(NonProductionPathCases))]
    public void ClassifyModuleBoundaryFile_Excludes_NonProduction_Paths(string relativePath, ModuleBoundaryExclusionKind expected)
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var filePath = Path.Combine(repoRoot, relativePath);

        var classification = ArchitectureTestHelpers.ClassifyModuleBoundaryFile(filePath, repoRoot);

        Assert.Multiple(() =>
        {
            Assert.That(classification.IsExcluded, Is.True);
            Assert.That(classification.IsProductionCode, Is.False);
            Assert.That(classification.ExclusionKind, Is.EqualTo(expected));
        });
    }

    [Test]
    public void PrepareCompilation_Uses_Only_Production_Source_Files_For_ModuleBoundary_Guards()
    {
        var (_, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Api");

        Assert.Multiple(() =>
        {
            Assert.That(syntaxTrees, Is.Not.Empty);
            Assert.That(compilation, Is.TypeOf<CSharpCompilation>());
            Assert.That(syntaxTrees.All(tree => ArchitectureTestHelpers.ClassifyModuleBoundaryFile(tree.FilePath).IsProductionCode), Is.True);
        });
    }
}
