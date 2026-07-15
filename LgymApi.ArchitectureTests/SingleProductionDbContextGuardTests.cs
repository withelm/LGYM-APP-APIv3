using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class SingleProductionDbContextGuardTests
{
    private static readonly string[] ProductionProjectRoots =
    [
        "LgymApi.Api",
        "LgymApi.Application",
        "LgymApi.Domain",
        "LgymApi.Infrastructure",
        "LgymApi.BackgroundWorker",
        "LgymApi.BackgroundWorker.Common",
        "LgymApi.DataSeeder",
        "LgymApi.Resources",
        "LgymApi.Resources.Generator"
    ];

    [Test]
    public void Production_Should_Expose_Exactly_One_DbContext_One_Migration_Stream_And_No_Schema_Split()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var productionFiles = ProductionProjectRoots
            .SelectMany(project => ArchitectureTestHelpers.EnumerateProjectSourceFiles(project))
            .ToList();

        var dbContexts = CollectProductionDbContexts(productionFiles, parseOptions);
        var migrationRoots = CollectMigrationRoots(repoRoot, ProductionProjectRoots);
        var schemaSplitViolations = CollectSchemaSplitViolations(repoRoot, productionFiles, parseOptions);

        Assert.That(
            dbContexts,
            Has.Count.EqualTo(1),
            BuildDbContextFailureMessage(dbContexts));

        Assert.That(
            dbContexts.Single().ContextType,
            Is.EqualTo("AppDbContext"),
            BuildDbContextFailureMessage(dbContexts));

        Assert.That(
            migrationRoots,
            Has.Count.EqualTo(1),
            BuildMigrationFailureMessage(migrationRoots));

        Assert.That(
            migrationRoots.Single(),
            Is.EqualTo("LgymApi.Infrastructure/Migrations"),
            BuildMigrationFailureMessage(migrationRoots));

        Assert.That(
            schemaSplitViolations,
            Is.Empty,
            "A schema-per-module production convention is not allowed. Violations:" + Environment.NewLine +
            string.Join(Environment.NewLine, schemaSplitViolations));

        var alternateRootMessage = BuildMigrationFailureMessage(["LgymApi.Infrastructure/Migrations", "LgymApi.Reporting/Migrations"]);
        Assert.That(alternateRootMessage, Does.Contain("alternate migration root"));
    }

    [Test]
    public void Guard_Message_Should_Name_Alternate_DbContext_And_Migration_Root_Details()
    {
        var message = BuildDbContextFailureMessage(
            new[]
            {
                new DbContextDeclaration("AppDbContext", "LgymApi.Infrastructure/Data/AppDbContext.cs"),
                new DbContextDeclaration("AlternateProductionDbContext", "LgymApi.Reporting/Data/AlternateProductionDbContext.cs")
            });

        var migrationMessage = BuildMigrationFailureMessage(
            new[]
            {
                "LgymApi.Infrastructure/Migrations",
                "LgymApi.Reporting/Migrations"
            });

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("AlternateProductionDbContext"));
            Assert.That(message, Does.Contain("LgymApi.Reporting/Data/AlternateProductionDbContext.cs"));
            Assert.That(message, Does.Contain("AppDbContext"));

            Assert.That(migrationMessage, Does.Contain("LgymApi.Reporting/Migrations"));
            Assert.That(migrationMessage, Does.Contain("alternate migration root"));
        });
    }

    private static List<DbContextDeclaration> CollectProductionDbContexts(IEnumerable<string> sourceFiles, CSharpParseOptions parseOptions)
    {
        var declarations = new List<DbContextDeclaration>();

        foreach (var file in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsDbContextDeclaration(typeDeclaration))
                {
                    continue;
                }

                declarations.Add(new DbContextDeclaration(typeDeclaration.Identifier.ValueText, file));
            }
        }

        return declarations;
    }

    private static List<string> CollectMigrationRoots(string repoRoot, IEnumerable<string> projectRoots)
    {
        var migrationFiles = projectRoots
            .SelectMany(project => ArchitectureTestHelpers.EnumerateProjectSourceFiles(project))
            .Where(path => ArchitectureTestHelpers.NormalizePath(path).Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return migrationFiles
            .Select(path =>
            {
                var relative = ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, path));
                var migrationIndex = relative.IndexOf("/Migrations/", StringComparison.OrdinalIgnoreCase);
                return migrationIndex < 0 ? relative : relative[..migrationIndex] + "/Migrations";
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> CollectSchemaSplitViolations(string repoRoot, IEnumerable<string> sourceFiles, CSharpParseOptions parseOptions)
    {
        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var methodName = GetMethodName(memberAccess.Name);
                if (methodName is not ("HasDefaultSchema" or "MigrationsHistoryTable" or "ToTable"))
                {
                    continue;
                }

                if (methodName == "HasDefaultSchema" && !HasNonNullSchemaArgument(invocation))
                {
                    continue;
                }

                if (methodName is "MigrationsHistoryTable" or "ToTable" && !HasExplicitSchemaArgument(invocation))
                {
                    continue;
                }

                var lineSpan = invocation.GetLocation().GetLineSpan();
                var lineNumber = lineSpan.StartLinePosition.Line + 1;
                var relativePath = ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, file));

                violations.Add($"{relativePath}:{lineNumber} -> {invocation}");
            }
        }

        return violations;
    }

    private static bool HasNonNullSchemaArgument(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count == 1)
        {
            return !IsNullSchemaExpression(invocation.ArgumentList.Arguments[0].Expression);
        }

        var schemaArgument = invocation.ArgumentList.Arguments[1].Expression;
        return !IsNullSchemaExpression(schemaArgument);
    }

    private static bool HasExplicitSchemaArgument(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count <= 1)
        {
            return false;
        }

        var schemaArgument = invocation.ArgumentList.Arguments[1].Expression;
        return !IsNullSchemaExpression(schemaArgument);
    }

    private static bool IsNullSchemaExpression(ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        if (expression is CastExpressionSyntax castExpression && castExpression.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return true;
        }

        return false;
    }

    private static bool IsDbContextDeclaration(ClassDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.BaseList == null)
        {
            return false;
        }

        return typeDeclaration.BaseList.Types
            .OfType<SimpleBaseTypeSyntax>()
            .Select(baseType => NormalizeType(baseType.Type))
            .Any(baseTypeName => baseTypeName.Equals("DbContext", StringComparison.Ordinal) || baseTypeName.EndsWith(".DbContext", StringComparison.Ordinal));
    }

    private static string BuildDbContextFailureMessage(IEnumerable<DbContextDeclaration> dbContexts)
    {
        return "Production DbContext guard failed. Exactly one production DbContext must exist and it must be AppDbContext." + Environment.NewLine +
               $"Rule: {nameof(SingleProductionDbContextGuardTests)}" + Environment.NewLine +
               string.Join(Environment.NewLine + Environment.NewLine, dbContexts.Select(context => context.ToDisplayString()));
    }

    private static string BuildMigrationFailureMessage(IEnumerable<string> migrationRoots)
    {
        return "Production migration stream guard failed. A second alternate migration root was detected when the solution must keep one shared production migration stream." + Environment.NewLine +
               $"Rule: {nameof(SingleProductionDbContextGuardTests)}" + Environment.NewLine +
               string.Join(Environment.NewLine + Environment.NewLine, migrationRoots.Select(root =>
                   $"Source file/path: {root}{Environment.NewLine}Detail: alternate migration root"));
    }

    private static string GetMethodName(SimpleNameSyntax methodNameSyntax)
    {
        return methodNameSyntax switch
        {
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => methodNameSyntax.ToString()
        };
    }

    private static string NormalizeType(TypeSyntax typeSyntax)
    {
        return typeSyntax
            .ToString()
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private sealed record DbContextDeclaration(string ContextType, string SourceFile)
    {
        public string ToDisplayString()
        {
            return $"Source file: {SourceFile}{Environment.NewLine}" +
                   $"Source symbol: {ContextType}";
        }
    }
}
