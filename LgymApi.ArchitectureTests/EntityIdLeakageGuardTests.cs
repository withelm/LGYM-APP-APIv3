using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class EntityIdLeakageGuardTests
{
    private static readonly HashSet<string> EntityIdPatterns = new()
    {
        "Id",
        "UserId",
        "PlanId",
        "ExerciseId",
        "WorkoutId",
        "GymId",
        "MeasurementId",
        "RecordId",
        "ScoreId",
        "AdminFlagId",
        "AppConfigId",
        "DashboardId",
        "RegistrationTokenId",
        "TrainingId",
        "MainRecordId",
        "EloRegistryId",
        "EmailNotificationSubscriptionId",
        "UserRoleId",
        "UserTutorialProgressId",
        "SupplementPlanItemId",
        "PlanDayExerciseId",
        "PlanDayId",
        "ExerciseTranslationId",
        "ExerciseScoreId"
    };

    private static readonly HashSet<string> OperationalGuidPatterns = new()
    {
        "CorrelationId",
        "TraceId",
        "RequestId",
        "SessionId",
        "envelopeId",
        "actionMessageId",
        "notificationId"
    };

    [Test]
    public void Entity_Id_Guid_Leakage_Guard_Must_Inspect_Production_Scope_And_Respect_Exclusions()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
        {
            // Skip test files, migrations, and build artifacts
            if (IsExcludedPath(tree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            // Check field declarations
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                CheckFieldDeclaration(field, tree, semanticModel, repoRoot, violations);
            }

            // Check property declarations
            foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                CheckPropertyDeclaration(property, tree, semanticModel, repoRoot, violations);
            }

            // Check method parameters
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                CheckMethodParameters(method, tree, semanticModel, repoRoot, violations);
            }

        }

        Assert.Multiple(() =>
        {
            Assert.That(
                violations,
                Is.Empty,
                "Raw Guid entity-ID leakage is forbidden in production scope. Violations:\n"
                + string.Join(Environment.NewLine, violations.Select(v => v.ToString())));

            Assert.That(
                violations.Any(v => v.Message.Contains("CorrelationId", StringComparison.OrdinalIgnoreCase)),
                Is.False,
                "Operational GUID names like CorrelationId must remain excluded from entity-ID leakage violations.");

            Assert.That(
                violations.Any(v => Normalize(v.File).Contains("IdempotencyKeyPolicy.cs", StringComparison.OrdinalIgnoreCase)),
                Is.False,
                "Operational correlation identifiers in IdempotencyKeyPolicy must not be flagged as entity-ID leakage.");
        });
    }

    private static void CheckFieldDeclaration(FieldDeclarationSyntax field, SyntaxTree tree, SemanticModel semanticModel, string repoRoot, List<Violation> violations)
    {
        var type = field.Declaration.Type;
        if (!IsRawGuidType(type, semanticModel))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            var fieldName = variable.Identifier.ValueText;
            if (IsEntityIdPattern(fieldName))
            {
                var line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                violations.Add(new Violation(
                    relativePath,
                    line,
                    $"Field '{fieldName}' uses raw Guid; use Id<TEntity> instead"));
            }
        }
    }

    private static void CheckPropertyDeclaration(PropertyDeclarationSyntax property, SyntaxTree tree, SemanticModel semanticModel, string repoRoot, List<Violation> violations)
    {
        var type = property.Type;
        if (!IsRawGuidType(type, semanticModel))
        {
            return;
        }

        var propertyName = property.Identifier.ValueText;
        if (IsEntityIdPattern(propertyName))
        {
            var line = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
            violations.Add(new Violation(
                relativePath,
                line,
                $"Property '{propertyName}' uses raw Guid; use Id<TEntity> instead"));
        }
    }

    private static void CheckMethodParameters(MethodDeclarationSyntax method, SyntaxTree tree, SemanticModel semanticModel, string repoRoot, List<Violation> violations)
    {
        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (!IsRawGuidType(parameter.Type, semanticModel))
            {
                continue;
            }

            var parameterName = parameter.Identifier.ValueText;
            if (IsEntityIdPattern(parameterName))
            {
                var line = parameter.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                violations.Add(new Violation(
                    relativePath,
                    line,
                    $"Method parameter '{parameterName}' uses raw Guid; use Id<TEntity> instead"));
            }
        }
    }

    private static bool IsRawGuidType(TypeSyntax? type, SemanticModel semanticModel)
    {
        if (type == null)
        {
            return false;
        }

        var typeString = type.ToString();

        // Check for simple Guid reference
        if (typeString == "Guid")
        {
            return true;
        }

        // Check for nullable Guid?
        if (typeString == "Guid?")
        {
            return true;
        }

        // Check for global::System.Guid
        if (typeString == "global::System.Guid" || typeString == "System.Guid")
        {
            return true;
        }

        return false;
    }

    private static bool IsEntityIdPattern(string name)
    {
        // Must end with one of the entity ID patterns
        foreach (var pattern in EntityIdPatterns)
        {
            if (name.Equals(pattern, StringComparison.OrdinalIgnoreCase) || name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // But NOT operational GUIDs (case-insensitive)
                foreach (var operationalPattern in OperationalGuidPatterns)
                {
                    if (name.Equals(operationalPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = Normalize(path);

        // Exclude test files
        if (normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/unittests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/integrationtests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/architecturetests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.unittests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.integrationtests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.architecturetests/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Exclude migrations
        if (normalized.Contains("/migrations/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Exclude typed-ID implementation
        if (normalized.Contains("ValueObjects/Id.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static (string RepoRoot, CSharpCompilation Compilation, IReadOnlyList<SyntaxTree> SyntaxTrees) PrepareCompilation()
    {
        var repoRoot = ResolveRepositoryRoot();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var sourceFiles = Directory
            .EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, "No C# source files found for entity ID leakage guard.");

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "EntityIdLeakageGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (repoRoot, compilation, syntaxTrees);
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MetadataReference> ResolveMetadataReferences()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.Location)
            .Where(location => !string.IsNullOrWhiteSpace(location) && File.Exists(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToList();
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LgymApi.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed record Violation(string File, int Line, string Message)
    {
        public override string ToString() => $"{File}:{Line} {Message}";
    }
}
