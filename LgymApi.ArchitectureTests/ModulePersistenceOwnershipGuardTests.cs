using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ModulePersistenceOwnershipGuardTests
{
    private static readonly string[] ValidRegistrationMethods =
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient"
    };

    [Test]
    public void Repositories_Configurations_And_Explicit_Registrar_Should_Stay_In_Their_Owning_Module()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var infrastructureFiles = ArchitectureTestHelpers.EnumerateProjectSourceFiles("LgymApi.Infrastructure");

        var repositoryFiles = infrastructureFiles
            .Where(path => Path.GetFileName(path).EndsWith("Repository.cs", StringComparison.Ordinal))
            .ToList();
        var serviceExtensionFiles = infrastructureFiles
            .Where(path => Path.GetFileName(path).EndsWith("ServiceCollectionExtensions.cs", StringComparison.Ordinal))
            .ToList();
        var configurationFiles = infrastructureFiles
            .Where(path =>
                ArchitectureTestHelpers.NormalizePath(path).Contains("/Data/Configurations/", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(path).EndsWith("EntityTypeConfiguration.cs", StringComparison.Ordinal))
            .ToList();
        var registrarFiles = infrastructureFiles
            .Where(path => Path.GetFileName(path).Equals("AppDbContextEntityTypeConfigurationRegistrar.cs", StringComparison.Ordinal))
            .ToList();
        var appDbContextFiles = infrastructureFiles
            .Where(path => Path.GetFileName(path).Equals("AppDbContext.cs", StringComparison.Ordinal))
            .ToList();

        Assert.That(repositoryFiles, Is.Not.Empty, "No repository files found for the persistence ownership guard.");
        Assert.That(serviceExtensionFiles, Is.Not.Empty, "No Infrastructure ServiceCollectionExtensions files found for the persistence ownership guard.");
        Assert.That(configurationFiles, Is.Not.Empty, "No module-owned EF configuration files found for the persistence ownership guard.");
        Assert.That(registrarFiles, Has.Count.EqualTo(1), "The explicit AppDbContext entity-type registrar must exist exactly once in Infrastructure/Data/Configurations.");
        Assert.That(appDbContextFiles, Has.Count.EqualTo(1), "The shared production AppDbContext root must exist exactly once.");

        var repositoryDeclarations = CollectRepositoryDeclarations(repositoryFiles, parseOptions);
        var registrations = CollectRegistrations(serviceExtensionFiles, parseOptions);
        var configurationDeclarations = CollectConfigurationDeclarations(configurationFiles, parseOptions);
        var registeredConfigurations = CollectRegisteredConfigurations(registrarFiles.Single(), parseOptions);

        var rootRegistrations = registrations
            .Where(registration => registration.Module is null)
            .ToList();

        Assert.That(
            rootRegistrations,
            Is.Empty,
            "Repository registrations must live in module-owned ServiceCollectionExtensions files, not the project-root composition shim." + Environment.NewLine +
            string.Join(Environment.NewLine, rootRegistrations.Select(registration => registration.ToString())));

        var duplicateRegistrations = registrations
            .GroupBy(registration => new { registration.Interface, registration.Implementation })
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .OrderBy(registration => registration.Interface, StringComparer.Ordinal)
            .ThenBy(registration => registration.Implementation, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            duplicateRegistrations,
            Is.Empty,
            "Duplicate repository registrations were found across module-owned helper files." + Environment.NewLine +
            string.Join(Environment.NewLine, duplicateRegistrations.Select(registration => registration.ToString())));

        var missingRepositories = repositoryDeclarations
            .Where(declaration => !registrations.Any(registration =>
                registration.Interface == declaration.Interface &&
                registration.Implementation == declaration.Implementation))
            .OrderBy(declaration => declaration.Interface, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Implementation, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missingRepositories,
            Is.Empty,
            "Every repository must be registered in Infrastructure ServiceCollectionExtensions." + Environment.NewLine +
            string.Join(Environment.NewLine, missingRepositories.Select(m => m.ToString())));

        var unownedConfigurations = configurationDeclarations
            .Where(configuration => configuration.Module is null)
            .ToList();

        Assert.That(
            unownedConfigurations,
            Is.Empty,
            "EF Core configuration files must stay under Data/Configurations/<Module>/ and not leak into alternate roots." + Environment.NewLine +
            string.Join(Environment.NewLine, unownedConfigurations.Select(configuration => configuration.ToString())));

        var staleRegistrarEntries = registeredConfigurations
            .Where(typeName => !configurationDeclarations.Any(configuration =>
                string.Equals(configuration.ConfigurationType, typeName, StringComparison.Ordinal)))
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            staleRegistrarEntries,
            Is.Empty,
            "AppDbContextEntityTypeConfigurationRegistrar must only register module-owned configuration types that still exist." + Environment.NewLine +
            string.Join(Environment.NewLine, staleRegistrarEntries));

        var missingRegistrarEntries = configurationDeclarations
            .Where(configuration => !registeredConfigurations.Contains(configuration.ConfigurationType, StringComparer.Ordinal))
            .OrderBy(configuration => configuration.ConfigurationType, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missingRegistrarEntries,
            Is.Empty,
            "Every module-owned EF Core configuration must be explicitly listed in AppDbContextEntityTypeConfigurationRegistrar." + Environment.NewLine +
            string.Join(Environment.NewLine, missingRegistrarEntries.Select(configuration => configuration.ToString())));

        var appDbContextContent = File.ReadAllText(appDbContextFiles.Single());
        Assert.That(
            appDbContextContent,
            Does.Contain("AppDbContextEntityTypeConfigurationRegistrar.Apply(modelBuilder);"),
            "The shared AppDbContext root must apply the explicit registrar instead of discovering module conventions by scanning.");

        var ownershipMessage = BuildOwnershipViolationMessage(
            "Identity & Accounts",
            "root composition shim",
            "IUserRepository -> UserRepository",
            "LgymApi.Infrastructure/ServiceCollectionExtensions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(ownershipMessage, Does.Contain("Identity & Accounts"));
            Assert.That(ownershipMessage, Does.Contain("root composition shim"));
            Assert.That(ownershipMessage, Does.Contain("IUserRepository -> UserRepository"));
            Assert.That(ownershipMessage, Does.Contain("LgymApi.Infrastructure/ServiceCollectionExtensions.cs"));
        });
    }

    private static List<RepositoryDeclaration> CollectRepositoryDeclarations(IEnumerable<string> repositoryFiles, CSharpParseOptions parseOptions)
    {
        var declarations = new List<RepositoryDeclaration>();

        foreach (var file in repositoryFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsConcreteRepository(typeDeclaration))
                {
                    continue;
                }

                var interfaceName = ResolveRepositoryInterface(typeDeclaration);
                if (interfaceName == null)
                {
                    continue;
                }

                declarations.Add(new RepositoryDeclaration(interfaceName, typeDeclaration.Identifier.ValueText, file));
            }
        }

        return declarations;
    }

    private static List<InfrastructureRegistration> CollectRegistrations(IEnumerable<string> serviceExtensionFiles, CSharpParseOptions parseOptions)
    {
        var registrations = new List<InfrastructureRegistration>();

        foreach (var file in serviceExtensionFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();
            var module = ArchitectureTestHelpers.GetServiceCollectionModuleName(file);

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

                if (genericName.TypeArgumentList.Arguments.Count >= 2)
                {
                    registrations.Add(new InfrastructureRegistration(
                        NormalizeType(genericName.TypeArgumentList.Arguments[0]),
                        NormalizeType(genericName.TypeArgumentList.Arguments[1]),
                        module,
                        file));
                    continue;
                }

                if (genericName.TypeArgumentList.Arguments.Count == 1)
                {
                    var interfaceType = NormalizeType(genericName.TypeArgumentList.Arguments[0]);
                    var factoryImplementation = ExtractFactoryImplementation(invocation);
                    if (factoryImplementation != null)
                    {
                        registrations.Add(new InfrastructureRegistration(interfaceType, factoryImplementation, module, file));
                    }
                }
            }
        }

        return registrations;
    }

    private static List<ConfigurationDeclaration> CollectConfigurationDeclarations(IEnumerable<string> configurationFiles, CSharpParseOptions parseOptions)
    {
        var declarations = new List<ConfigurationDeclaration>();

        foreach (var file in configurationFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();
            var module = ArchitectureTestHelpers.GetInfrastructureConfigurationModuleName(file);

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsConcreteEntityTypeConfiguration(typeDeclaration))
                {
                    continue;
                }

                declarations.Add(new ConfigurationDeclaration(module, typeDeclaration.Identifier.ValueText, file));
            }
        }

        return declarations;
    }

    private static HashSet<string> CollectRegisteredConfigurations(string registrarFile, CSharpParseOptions parseOptions)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(registrarFile), parseOptions, registrarFile);
        var root = tree.GetCompilationUnitRoot();

        return root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(objectCreation => NormalizeType(objectCreation.Type))
            .Where(typeName => typeName.EndsWith("EntityTypeConfiguration", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string BuildOwnershipViolationMessage(string ownerModule, string nonOwnerModule, string detail, string alternateRoot)
    {
        return "Module persistence ownership failed:" + Environment.NewLine +
               $"Owner module: {ownerModule}" + Environment.NewLine +
               $"Non-owner: {nonOwnerModule}" + Environment.NewLine +
               $"Detail: {detail}" + Environment.NewLine +
               $"Alternate root: {alternateRoot}";
    }

    private static bool IsConcreteRepository(ClassDeclarationSyntax typeDeclaration)
    {
        if (!typeDeclaration.Identifier.ValueText.EndsWith("Repository", StringComparison.Ordinal))
        {
            return false;
        }

        return !typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static bool IsConcreteEntityTypeConfiguration(ClassDeclarationSyntax typeDeclaration)
    {
        if (!typeDeclaration.Identifier.ValueText.EndsWith("EntityTypeConfiguration", StringComparison.Ordinal))
        {
            return false;
        }

        return !typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static string? ResolveRepositoryInterface(ClassDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.BaseList == null)
        {
            return null;
        }

        foreach (var baseType in typeDeclaration.BaseList.Types.OfType<SimpleBaseTypeSyntax>())
        {
            var identifier = baseType.Type.ToString();
            if (identifier.StartsWith('I') && identifier.EndsWith("Repository", StringComparison.Ordinal))
            {
                return NormalizeType(baseType.Type);
            }
        }

        return null;
    }

    private static string NormalizeType(TypeSyntax typeSyntax)
    {
        return typeSyntax
            .ToString()
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string? ExtractFactoryImplementation(InvocationExpressionSyntax invocation)
    {
        var objectCreation = invocation.ArgumentList.Arguments
            .SelectMany(argument => argument.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            .FirstOrDefault();

        return objectCreation == null ? null : NormalizeType(objectCreation.Type);
    }

    private sealed record RepositoryDeclaration(string Interface, string Implementation, string SourceFile)
    {
        public override string ToString() => $"{SourceFile}: {Interface} -> {Implementation}";
    }

    private sealed record InfrastructureRegistration(string Interface, string Implementation, string? Module, string SourceFile)
    {
        public override string ToString() => $"{SourceFile} [{Module ?? "root"}]: {Interface} -> {Implementation}";
    }

    private sealed record ConfigurationDeclaration(string? Module, string ConfigurationType, string SourceFile)
    {
        public override string ToString() => $"{SourceFile} [{Module ?? "root"}]: {ConfigurationType}";
    }
}
