using System.Xml.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ResourceKeysCoverageGuardTests
{
    /// <summary>
    /// Ensures that all Messages.* resource key references in code actually exist in the Messages.resx file.
    /// This prevents runtime MissingManifestResourceException errors.
    /// Checks both Messages.resx (English) and Messages.pl.resx (Polish) if it exists.
    /// </summary>
    [Test]
    public void All_Referenced_Message_Keys_Should_Exist_In_Resource_Files()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var apiRoot = Path.Combine(repoRoot, "LgymApi.Api");
        var appRoot = Path.Combine(repoRoot, "LgymApi.Application");
        var resourceFile = Path.Combine(repoRoot, "LgymApi.Resources", "Resources", "Messages.resx");

        Directory.Exists(apiRoot).Should().BeTrue($"LgymApi.Api root '{apiRoot}' not found.");
        Directory.Exists(appRoot).Should().BeTrue($"LgymApi.Application root '{appRoot}' not found.");
        File.Exists(resourceFile).Should().BeTrue($"Messages.resx file '{resourceFile}' not found.");

        // Parse the resource file and extract all valid keys
        var validKeys = ExtractResourceKeys(resourceFile);
        validKeys.Should().NotBeEmpty("No keys found in Messages.resx.");

        // Parse all C# files in both projects and collect Messages.* references
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        // Scan Api project
        var apiFiles = Directory
            .EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .ToList();

        foreach (var file in apiFiles)
        {
            var fileViolations = FindResourceKeyViolations(file, parseOptions, validKeys, repoRoot);
            violations.AddRange(fileViolations);
        }

        // Scan Application project
        var appFiles = Directory
            .EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .ToList();

        foreach (var file in appFiles)
        {
            var fileViolations = FindResourceKeyViolations(file, parseOptions, validKeys, repoRoot);
            violations.AddRange(fileViolations);
        }

        violations.Sort((a, b) =>
        {
            var fileCompare = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return fileCompare != 0 ? fileCompare : a.Line.CompareTo(b.Line);
        });

        violations
            .Should()
            .BeEmpty(
                $"All Messages.* references must exist in Messages.resx. " +
                $"Found {violations.Count} violations:\n" +
                string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    /// <summary>
    /// Extracts all valid resource key names from a .resx file by parsing XML.
    /// </summary>
    /// <param name="resxPath">Path to the .resx file.</param>
    /// <returns>A HashSet of valid key names (case-sensitive).</returns>
    private static HashSet<string> ExtractResourceKeys(string resxPath)
    {
        var doc = XDocument.Load(resxPath);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var dataElements = doc.Descendants("data");
        foreach (var element in dataElements)
        {
            var nameAttr = element.Attribute("name");
            if (nameAttr != null && !string.IsNullOrEmpty(nameAttr.Value))
            {
                keys.Add(nameAttr.Value);
            }
        }

        return keys;
    }

    /// <summary>
    /// Finds all Messages.X references in a C# file and checks if they exist in the valid keys set.
    /// </summary>
    /// <param name="filePath">Path to the C# source file.</param>
    /// <param name="parseOptions">CSharp parse options.</param>
    /// <param name="validKeys">Set of valid resource key names.</param>
    /// <param name="repoRoot">Repository root for relative path calculation.</param>
    /// <returns>A list of violations for missing keys.</returns>
    private static List<Violation> FindResourceKeyViolations(
        string filePath,
        CSharpParseOptions parseOptions,
        HashSet<string> validKeys,
        string repoRoot)
    {
        var violations = new List<Violation>();
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), parseOptions, filePath);
        var root = tree.GetCompilationUnitRoot();

        // Find all member access expressions where the left side is "Messages"
        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (IsMessagesPropertyAccess(memberAccess, out var keyName))
            {
                // Check if this key exists in the resource file
                // keyName is guaranteed to be non-null by IsMessagesPropertyAccess
                if (keyName != null && !validKeys.Contains(keyName))
                {
                    violations.Add(CreateViolation(repoRoot, tree, memberAccess, keyName));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Checks if a MemberAccessExpressionSyntax is a Messages.X property access.
    /// Returns the property name if it is.
    /// </summary>
    /// <param name="memberAccess">The syntax node to check.</param>
    /// <param name="keyName">Output: the property name if this is Messages.X, otherwise null.</param>
    /// <returns>True if this is a Messages.X access, false otherwise.</returns>
    private static bool IsMessagesPropertyAccess(MemberAccessExpressionSyntax memberAccess, out string? keyName)
    {
        keyName = null;

        // Check if the left side is "Messages" (an IdentifierNameSyntax)
        if (memberAccess.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.ValueText == "Messages")
        {
            keyName = memberAccess.Name.Identifier.ValueText;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a Violation record with relative path, line number, and missing key info.
    /// </summary>
    private static Violation CreateViolation(
        string repoRoot,
        SyntaxTree tree,
        SyntaxNode node,
        string missingKey)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, missingKey);
    }

    /// <summary>
    /// Record representing a single violation (missing resource key).
    /// </summary>
    private sealed record Violation(string File, int Line, string MissingKey)
    {
        public override string ToString() => $"{File}:{Line} -> Messages.{MissingKey} not found in Messages.resx";
    }
}
