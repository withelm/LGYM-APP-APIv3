using System.Reflection;
using FluentAssertions;
using NUnit.Framework;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class PaginationArchitectureGuardTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(LgymApi.Application.Pagination.Pagination<>).Assembly;

    private static readonly string RepositorySourcePath = ResolveSourcePath(
        "LgymApi.Infrastructure", "Repositories", "TrainerRelationshipRepository.cs");

    [Test]
    public void PaginationContracts_DoNotReferenceGridifyOrEfTypes()
    {
        var paginationTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace is "LgymApi.Application.Pagination")
            .ToList();

        paginationTypes.Should().NotBeEmpty("the Application.Pagination namespace should contain types");

        var violations = new List<string>();

        foreach (var type in paginationTypes)
        {
            CheckTypeForForbiddenReferences(type, violations);
        }

        violations.Should().BeEmpty(
            "Application.Pagination contracts must not depend on Gridify or EF Core types. " +
            $"Violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Test]
    public void ApplicationAssembly_DoesNotReferenceGridifyPackage()
    {
        var referencedAssemblies = ApplicationAssembly.GetReferencedAssemblies();

        var gridifyReferences = referencedAssemblies
            .Where(a => a.Name != null &&
                        a.Name.Contains("Gridify", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.FullName)
            .ToList();

        gridifyReferences.Should().BeEmpty(
            "LgymApi.Application must not reference Gridify assemblies — " +
            "Gridify is an infrastructure concern");
    }

    [Test]
    public void ApplicationAssembly_DoesNotReferenceEfCorePackage()
    {
        var referencedAssemblies = ApplicationAssembly.GetReferencedAssemblies();

        var efCoreReferences = referencedAssemblies
            .Where(a => a.Name != null &&
                        a.Name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.FullName)
            .ToList();

        efCoreReferences.Should().BeEmpty(
            "LgymApi.Application must not reference EF Core assemblies — " +
            "EF Core is an infrastructure concern");
    }

    [Test]
    public void TrainerDashboardGridifyPagination_DoesNotMaterializeBeforePaging()
    {
        Assert.That(File.Exists(RepositorySourcePath), Is.True,
            $"TrainerRelationshipRepository source not found at '{RepositorySourcePath}'.");

        var sourceCode = File.ReadAllText(RepositorySourcePath);

        var methodBody = ExtractMethodBody(sourceCode, "GetDashboardTraineesAsync");

        methodBody.Should().NotBeNull(
            "GetDashboardTraineesAsync method must exist in TrainerRelationshipRepository");

        var gridifyCallIndex = methodBody!.IndexOf("_gridifyExecutionService.ExecuteAsync", StringComparison.Ordinal);
        gridifyCallIndex.Should().BeGreaterThan(-1,
            "GetDashboardTraineesAsync should delegate to GridifyExecutionService.ExecuteAsync");

        var beforeGridify = methodBody[..gridifyCallIndex];

        beforeGridify.Should().NotContain("ToListAsync",
            "must not materialize query before passing to GridifyExecutionService — " +
            "this was the old in-memory sorting anti-pattern");

        beforeGridify.Should().NotContain("ToList()",
            "must not materialize query synchronously before passing to GridifyExecutionService");

        beforeGridify.Should().NotContain("ToArrayAsync",
            "must not materialize query to array before passing to GridifyExecutionService");
    }

    private static void CheckTypeForForbiddenReferences(Type type, List<string> violations)
    {
        var forbiddenNamespacePrefixes = new[]
        {
            "Gridify",
            "Microsoft.EntityFrameworkCore"
        };

        // Check base type
        if (type.BaseType is { } baseType)
        {
            CheckSingleType(baseType, type, "base type", forbiddenNamespacePrefixes, violations);
        }

        // Check implemented interfaces
        foreach (var iface in type.GetInterfaces())
        {
            CheckSingleType(iface, type, "interface", forbiddenNamespacePrefixes, violations);
        }

        // Check method return types and parameter types
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            CheckSingleType(method.ReturnType, type, $"method '{method.Name}' return type", forbiddenNamespacePrefixes, violations);

            foreach (var param in method.GetParameters())
            {
                CheckSingleType(param.ParameterType, type, $"method '{method.Name}' parameter '{param.Name}'", forbiddenNamespacePrefixes, violations);
            }
        }

        // Check property types
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var prop in properties)
        {
            CheckSingleType(prop.PropertyType, type, $"property '{prop.Name}'", forbiddenNamespacePrefixes, violations);
        }

        // Check constructor parameters
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var param in ctor.GetParameters())
            {
                CheckSingleType(param.ParameterType, type, $"constructor parameter '{param.Name}'", forbiddenNamespacePrefixes, violations);
            }
        }
    }

    private static void CheckSingleType(Type typeToCheck, Type ownerType, string context,
        string[] forbiddenPrefixes, List<string> violations)
    {
        var typesToInspect = new List<Type> { typeToCheck };

        if (typeToCheck.IsGenericType)
        {
            typesToInspect.AddRange(typeToCheck.GetGenericArguments());
        }

        foreach (var t in typesToInspect)
        {
            var ns = t.Namespace;
            if (ns is null)
            {
                continue;
            }

            foreach (var prefix in forbiddenPrefixes)
            {
                if (ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        $"  {ownerType.FullName} -> {context}: references {t.FullName} (namespace: {ns})");
                }
            }
        }
    }

    private static string? ExtractMethodBody(string sourceCode, string methodName)
    {
        var methodIndex = sourceCode.IndexOf(methodName, StringComparison.Ordinal);
        if (methodIndex < 0) return null;

        var braceStart = sourceCode.IndexOf('{', methodIndex);
        if (braceStart < 0) return null;

        var depth = 0;
        for (var i = braceStart; i < sourceCode.Length; i++)
        {
            if (sourceCode[i] == '{') depth++;
            else if (sourceCode[i] == '}') depth--;

            if (depth == 0)
            {
                return sourceCode[braceStart..(i + 1)];
            }
        }

        return null;
    }

    private static string ResolveSourcePath(params string[] pathSegments)
    {
        var repoRoot = ResolveRepositoryRoot();
        return Path.Combine(new[] { repoRoot }.Concat(pathSegments).ToArray());
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

        throw new InvalidOperationException("Unable to locate repository root (LgymApi.sln).");
    }
}
