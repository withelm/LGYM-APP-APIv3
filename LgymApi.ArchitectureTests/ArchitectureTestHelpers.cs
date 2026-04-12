using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

/// <summary>
/// Shared utility methods for architecture guard tests (Roslyn-based pattern validation).
/// Extracted from common patterns in existing guard test files to reduce duplication.
/// </summary>
public static class ArchitectureTestHelpers
{
    /// <summary>
    /// Resolves the solution root directory by walking up from the current test execution directory
    /// until it finds a file named "LgymApi.sln".
    /// </summary>
    /// <returns>The absolute path to the solution root directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the repository root cannot be located.</exception>
    public static string ResolveRepositoryRoot()
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

    /// <summary>
    /// Resolves all assembly metadata references currently loaded in the AppDomain.
    /// Used to populate Roslyn compilation metadata for semantic analysis.
    /// </summary>
    /// <returns>A list of MetadataReference objects for all non-dynamic assemblies.</returns>
    public static List<MetadataReference> ResolveMetadataReferences()
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

    /// <summary>
    /// Checks whether a file path is located in build artifacts (bin or obj directories).
    /// Used to exclude compiled output from source analysis.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the path is in bin or obj; false otherwise.</returns>
    public static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses all C# source files in a given project directory and returns their syntax trees.
    /// Filters out build artifacts automatically.
    /// </summary>
    /// <param name="projectRelativePath">Relative path from repository root to the project directory (e.g., "LgymApi.Api").</param>
    /// <returns>A list of SyntaxTree objects for all source files in the project.</returns>
    public static List<SyntaxTree> ParseProjectSources(string projectRelativePath)
    {
        var repoRoot = ResolveRepositoryRoot();
        var projectRoot = Path.Combine(repoRoot, projectRelativePath);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var sourceFiles = Directory
            .EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        return syntaxTrees;
    }

    /// <summary>
    /// Creates a CSharpCompilation from a list of syntax trees with full metadata references.
    /// Suitable for semantic analysis requiring type symbol resolution.
    /// </summary>
    /// <param name="trees">The syntax trees to include in the compilation.</param>
    /// <returns>A CSharpCompilation object with metadata references populated.</returns>
    public static CSharpCompilation CreateCompilation(List<SyntaxTree> trees)
    {
        return CSharpCompilation.Create(
            "ArchitectureGuardCompilation",
            trees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
