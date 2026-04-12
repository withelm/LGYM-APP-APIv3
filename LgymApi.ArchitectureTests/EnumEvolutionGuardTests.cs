using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class EnumEvolutionGuardTests
{
    /// <summary>
    /// Ensures that all enum members in LgymApi.Domain/Enums/ have explicit numeric values.
    /// This prevents accidental member reordering from changing persisted integer values.
    /// Per AGENTS.md: "Do not reorder or renumber existing enum members."
    /// 
    /// ALLOWED: MyValue = 0, MyOtherValue = 1
    /// FORBIDDEN: MyValue (implicit value), MyOtherValue (implicit value)
    /// </summary>
    [Test]
    public void All_Enum_Members_Should_Have_Explicit_Values()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var enumsRoot = Path.Combine(repoRoot, "LgymApi.Domain", "Enums");

        Assert.That(Directory.Exists(enumsRoot), Is.True, $"Enums root '{enumsRoot}' not found.");

        var enumFiles = Directory
            .EnumerateFiles(enumsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(enumFiles, Is.Not.Empty, "No enum files found for enum evolution guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        foreach (var enumFile in enumFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(enumFile), parseOptions, enumFile);
            var root = tree.GetCompilationUnitRoot();

            // Find all enum declarations
            foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                // Check each enum member
                foreach (var member in enumDecl.Members)
                {
                    // If the member doesn't have an explicit value assignment (EqualsValueClauseSyntax), it's a violation
                    var hasExplicitValue = member.ChildNodes().OfType<EqualsValueClauseSyntax>().Any();
                    if (!hasExplicitValue)
                    {
                        violations.Add(CreateViolation(repoRoot, tree, enumDecl, member));
                    }
                }
            }
        }

        violations.Sort((a, b) =>
        {
            var fileCompare = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return fileCompare != 0 ? fileCompare : a.Line.CompareTo(b.Line);
        });

        Assert.That(
            violations.Count,
            Is.EqualTo(0),
            $"All enum members must have explicit numeric values. " +
            $"Found {violations.Count} violations:\n" +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    /// <summary>
    /// Creates a Violation record with relative path, enum name, member name, and line number.
    /// </summary>
    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, EnumDeclarationSyntax enumDecl, EnumMemberDeclarationSyntax member)
    {
        var span = tree.GetLineSpan(member.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        var enumName = enumDecl.Identifier.ValueText;
        var memberName = member.Identifier.ValueText;

        return new Violation(relativePath, enumName, memberName, line);
    }

    /// <summary>
    /// Record representing a single violation (enum member without explicit value).
    /// </summary>
    private sealed record Violation(string File, string EnumName, string MemberName, int Line)
    {
        public override string ToString() => $"{File}:{Line} -> {EnumName}.{MemberName} (missing explicit value)";
    }
}
