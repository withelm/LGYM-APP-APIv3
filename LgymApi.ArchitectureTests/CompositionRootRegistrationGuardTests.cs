using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CompositionRootRegistrationGuardTests
{
    private static readonly string[] RequiredCompositionMethods =
    {
        "AddIdentityModule",
        "AddTrainingPlanningModule",
        "AddWorkoutAndProgressModule",
        "AddCoachingModule",
        "AddNutritionModule",
        "AddReportingModule",
        "AddPlatformServices",
        "AddIdentityInfrastructure",
        "AddTrainingPlanningInfrastructure",
        "AddWorkoutProgressInfrastructure",
        "AddCoachingInfrastructure",
        "AddNutritionInfrastructure",
        "AddReportingInfrastructure",
        "AddNotificationsModule",
        "AddBackgroundWorkerServices",
        "AddApplicationMapping"
    };

    private static readonly string[] LegacyCompositionMethods =
    {
        "AddApplicationServices",
        "AddInfrastructure"
    };

    [Test]
    public void Program_Should_Register_Required_Composition_Methods()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var programPath = Path.Combine(repoRoot, "LgymApi.Api", "Program.cs");

        Assert.That(File.Exists(programPath), Is.True, $"Program.cs not found at '{programPath}'");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var programContent = File.ReadAllText(programPath);
        var tree = CSharpSyntaxTree.ParseText(programContent, parseOptions, programPath);
        var root = tree.GetCompilationUnitRoot();

        var invocations = root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => ExtractMethodName(invocation))
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        var missing = RequiredCompositionMethods
            .Where(method => !invocations.Contains(method))
            .ToList();

        var legacyCalls = LegacyCompositionMethods
            .Where(method => invocations.Contains(method))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(
                missing,
                Is.Empty,
                $"Program.cs must call the following composition methods: {string.Join(", ", RequiredCompositionMethods)}. " +
                $"Missing: {string.Join(", ", missing)}");

            Assert.That(
                legacyCalls,
                Is.Empty,
                $"Program.cs must not call the removed composition shims: {string.Join(", ", LegacyCompositionMethods)}. " +
                $"Found: {string.Join(", ", legacyCalls)}");
        });
    }

    /// <summary>
    /// Extracts the method name from an invocation expression.
    /// Handles both direct method calls (e.g., AddApplication()) and chained calls (e.g., builder.Services.AddApplication()).
    /// </summary>
    private static string? ExtractMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            // Direct method call: AddApplication()
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            // Chained call: something.AddApplication()
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            // Generic method call: AddApplication<T>()
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null
        };
    }
}
