using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class MapperRegistrationGuardTests
{
    [Test]
    public void Explicit_Map_Usages_Should_Have_Declared_CreateMap()
    {
        var repoRoot = ResolveRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(repoRoot, "LgymApi.Api"),
            Path.Combine(repoRoot, "LgymApi.Application")
        };

        var sourceFiles = sourceRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, "No source files found for mapper guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var declaredMappings = new HashSet<MapPair>();
        var usedMappings = new List<MapUsage>();

        foreach (var file in sourceFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, options: parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();
            var aliases = ResolveTypeAliases(root);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryGetMapPair(invocation, "CreateMap", aliases, out var createMapPair, out var sourceType, out var targetType))
                {
                    continue;
                }

                if (ContainsOpenGenericTypeParameter(invocation, sourceType, targetType))
                {
                    continue;
                }

                declaredMappings.Add(createMapPair);
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                MapPair mapPair;
                TypeSyntax sourceType;
                TypeSyntax targetType;

                var isMapInvocation = TryGetMapPair(invocation, "Map", aliases, out mapPair, out sourceType, out targetType)
                    || TryGetMapPair(invocation, "MapList", aliases, out mapPair, out sourceType, out targetType);

                if (!isMapInvocation)
                {
                    continue;
                }

                if (ContainsOpenGenericTypeParameter(invocation, sourceType, targetType))
                {
                    continue;
                }

                usedMappings.Add(CreateUsage(repoRoot, tree, invocation, mapPair));
            }
        }

        var violations = usedMappings
            .Where(usage => !declaredMappings.Contains(usage.Pair))
            .DistinctBy(usage => usage.Pair)
            .OrderBy(usage => usage.File, StringComparer.Ordinal)
            .ThenBy(usage => usage.Line)
            .ToList();

        Assert.That(
            violations,
            Is.Empty,
            "Every explicit mapper usage Map<TSource, TTarget>/MapList<TSource, TTarget> must have a corresponding CreateMap<TSource, TTarget>. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool TryGetMapPair(
        InvocationExpressionSyntax invocation,
        string methodName,
        IReadOnlyDictionary<string, string> aliases,
        out MapPair pair,
        out TypeSyntax sourceType,
        out TypeSyntax targetType)
    {
        pair = default!;
        sourceType = null!;
        targetType = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
        {
            return false;
        }

        if (!string.Equals(genericName.Identifier.ValueText, methodName, StringComparison.Ordinal))
        {
            return false;
        }

        if (genericName.TypeArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        sourceType = genericName.TypeArgumentList.Arguments[0];
        targetType = genericName.TypeArgumentList.Arguments[1];
        pair = new MapPair(NormalizeType(sourceType, aliases), NormalizeType(targetType, aliases));
        return true;
    }

    private static Dictionary<string, string> ResolveTypeAliases(CompilationUnitSyntax root)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var usingDirective in root.Usings)
        {
            if (usingDirective.Alias == null || usingDirective.Name == null)
            {
                continue;
            }

            aliases[usingDirective.Alias.Name.Identifier.ValueText] = NormalizeType(usingDirective.Name, aliases);
        }

        return aliases;
    }

    private static bool ContainsOpenGenericTypeParameter(InvocationExpressionSyntax invocation, TypeSyntax sourceType, TypeSyntax targetType)
    {
        var inScopeTypeParameters = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in invocation.Ancestors().OfType<MethodDeclarationSyntax>())
        {
            if (method.TypeParameterList == null)
            {
                continue;
            }

            foreach (var parameter in method.TypeParameterList.Parameters)
            {
                inScopeTypeParameters.Add(parameter.Identifier.ValueText);
            }
        }

        foreach (var localFunction in invocation.Ancestors().OfType<LocalFunctionStatementSyntax>())
        {
            if (localFunction.TypeParameterList == null)
            {
                continue;
            }

            foreach (var parameter in localFunction.TypeParameterList.Parameters)
            {
                inScopeTypeParameters.Add(parameter.Identifier.ValueText);
            }
        }

        foreach (var type in invocation.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            if (type.TypeParameterList == null)
            {
                continue;
            }

            foreach (var parameter in type.TypeParameterList.Parameters)
            {
                inScopeTypeParameters.Add(parameter.Identifier.ValueText);
            }
        }

        if (inScopeTypeParameters.Count == 0)
        {
            return false;
        }

        return ReferencesTypeParameter(sourceType, inScopeTypeParameters)
            || ReferencesTypeParameter(targetType, inScopeTypeParameters);
    }

    private static bool ReferencesTypeParameter(TypeSyntax type, HashSet<string> inScopeTypeParameters)
    {
        return type
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => inScopeTypeParameters.Contains(identifier.Identifier.ValueText));
    }

    private static string NormalizeType(TypeSyntax type, IReadOnlyDictionary<string, string> aliases)
    {
        return type switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.ValueText,
            IdentifierNameSyntax identifier => aliases.TryGetValue(identifier.Identifier.ValueText, out var aliased)
                ? aliased
                : identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => NormalizeType(qualified.Right, aliases),
            AliasQualifiedNameSyntax aliasQualified => NormalizeType(aliasQualified.Name, aliases),
            GenericNameSyntax generic => $"{generic.Identifier.ValueText}<{string.Join(",", generic.TypeArgumentList.Arguments.Select(arg => NormalizeType(arg, aliases)))}>",
            NullableTypeSyntax nullable => $"{NormalizeType(nullable.ElementType, aliases)}?",
            ArrayTypeSyntax array => $"{NormalizeType(array.ElementType, aliases)}{string.Concat(array.RankSpecifiers.Select(rank => $"[{new string(',', rank.Sizes.SeparatorCount)}]"))}",
            TupleTypeSyntax tuple => $"({string.Join(",", tuple.Elements.Select(element => NormalizeType(element.Type, aliases)))})",
            _ => type.ToString().Replace("global::", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal)
        };
    }

    private static MapUsage CreateUsage(string repoRoot, SyntaxTree tree, SyntaxNode node, MapPair pair)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new MapUsage(relativePath, line, pair);
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
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

    private sealed record MapPair(string Source, string Target)
    {
        public override string ToString() => $"{Source} -> {Target}";
    }

    private sealed record MapUsage(string File, int Line, MapPair Pair)
    {
        public override string ToString() => $"{File}:{Line} [{Pair}]";
    }
}
