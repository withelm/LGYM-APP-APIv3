using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class InfrastructureServiceRegistrationGuardTests
{
    private static readonly string[] ValidRegistrationMethods =
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient"
    };

    [Test]
    public void InfrastructureServiceRegistration_ConcreteInfrastructureServices_Should_Be_Registered_In_CompositionRoot()
    {
        var repoRoot = ResolveRepositoryRoot();
        var infrastructureServicesRoot = Path.Combine(repoRoot, "LgymApi.Infrastructure", "Services");
        var infrastructureServiceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Infrastructure", "ServiceCollectionExtensions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(infrastructureServicesRoot), Is.True, $"Infrastructure services root '{infrastructureServicesRoot}' not found.");
            Assert.That(File.Exists(infrastructureServiceExtensionsPath), Is.True, $"Infrastructure ServiceCollectionExtensions file '{infrastructureServiceExtensionsPath}' not found.");
        });

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var concreteServices = ExtractConcreteInfrastructureServices(infrastructureServicesRoot, parseOptions);

        Assert.That(concreteServices, Is.Not.Empty, "No concrete infrastructure services detected for DI registration guard.");

        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(infrastructureServiceExtensionsPath),
            parseOptions,
            infrastructureServiceExtensionsPath);

        var registrations = ExtractRegisteredConcreteServiceTypes(serviceExtensionsTree, concreteServices);

        var missing = concreteServices
            .Where(serviceName => !registrations.Contains(serviceName))
            .OrderBy(serviceName => serviceName, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missing,
            Is.Empty,
            "Every concrete infrastructure service must be registered in Infrastructure ServiceCollectionExtensions." + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(serviceName => $"Missing infrastructure service registration: {serviceName}")));
    }

    [Test]
    public void InfrastructureServiceRegistration_Factory_Interface_Registration_Should_Not_Cause_False_Positive()
    {
        var repoRoot = ResolveRepositoryRoot();
        var infrastructureServicesRoot = Path.Combine(repoRoot, "LgymApi.Infrastructure", "Services");
        var infrastructureServiceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Infrastructure", "ServiceCollectionExtensions.cs");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var concreteServices = ExtractConcreteInfrastructureServices(infrastructureServicesRoot, parseOptions);
        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(infrastructureServiceExtensionsPath),
            parseOptions,
            infrastructureServiceExtensionsPath);

        var registrationCandidates = ExtractRegistrationCandidates(serviceExtensionsTree).ToList();
        Assert.That(
            registrationCandidates.Any(candidate =>
                candidate.CandidateTypeName.Equals("IEmailSender", StringComparison.Ordinal) &&
                candidate.ArgumentCount == 1),
            Is.True,
            "Fixture assumption failed: expected AddScoped<IEmailSender>(sp => ...) factory registration in Infrastructure ServiceCollectionExtensions.");

        var concreteRegistrations = ExtractRegisteredConcreteServiceTypes(serviceExtensionsTree, concreteServices);

        Assert.Multiple(() =>
        {
            Assert.That(concreteRegistrations.Contains("SmtpEmailSender"), Is.True, "Expected concrete self-registration for SmtpEmailSender.");
            Assert.That(concreteRegistrations.Contains("DummyEmailSender"), Is.True, "Expected concrete self-registration for DummyEmailSender.");
            Assert.That(concreteRegistrations.Contains("IEmailSender"), Is.False, "Factory interface registration must not be treated as concrete service registration.");
        });
    }

    private static HashSet<string> ExtractConcreteInfrastructureServices(string servicesRoot, CSharpParseOptions parseOptions)
    {
        var declarations = new HashSet<string>(StringComparer.Ordinal);
        var serviceFiles = Directory
            .EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path));

        foreach (var file in serviceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsConcreteService(typeDeclaration))
                {
                    continue;
                }

                declarations.Add(typeDeclaration.Identifier.ValueText);
            }
        }

        return declarations;
    }

    private static HashSet<string> ExtractRegisteredConcreteServiceTypes(SyntaxTree serviceExtensionsTree, ISet<string> concreteServices)
    {
        var registrations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in ExtractRegistrationCandidates(serviceExtensionsTree))
        {
            if (concreteServices.Contains(candidate.CandidateTypeName))
            {
                registrations.Add(candidate.CandidateTypeName);
            }
        }

        return registrations;
    }

    private static IEnumerable<RegistrationCandidate> ExtractRegistrationCandidates(SyntaxTree serviceExtensionsTree)
    {
        var root = serviceExtensionsTree.GetCompilationUnitRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            {
                continue;
            }

            if (!ValidRegistrationMethods.Contains(genericName.Identifier.ValueText))
            {
                continue;
            }

            var arguments = genericName.TypeArgumentList.Arguments;
            if (arguments.Count == 1)
            {
                yield return new RegistrationCandidate(GetSimpleTypeName(arguments[0]), 1);
                continue;
            }

            if (arguments.Count >= 2)
            {
                yield return new RegistrationCandidate(GetSimpleTypeName(arguments[1]), arguments.Count);
            }
        }
    }

    private static bool IsConcreteService(ClassDeclarationSyntax typeDeclaration)
    {
        return !typeDeclaration.Modifiers.Any(modifier =>
            modifier.IsKind(SyntaxKind.AbstractKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword));
    }

    private static string GetSimpleTypeName(TypeSyntax typeSyntax)
    {
        return typeSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetSimpleTypeName(qualified.Right),
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => typeSyntax
                .ToString()
                .Replace("global::", string.Empty, StringComparison.Ordinal)
                .Split(new[] { '.', '<' }, StringSplitOptions.RemoveEmptyEntries)[^1]
                .Replace(">", string.Empty, StringComparison.Ordinal)
        };
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
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

    private sealed record RegistrationCandidate(string CandidateTypeName, int ArgumentCount);
}
