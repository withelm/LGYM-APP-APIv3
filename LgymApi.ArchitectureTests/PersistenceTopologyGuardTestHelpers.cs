using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

internal static class PersistenceTopologyGuardTestHelpers
{
    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";
    private const string DbSetMetadataName = "Microsoft.EntityFrameworkCore.DbSet<T>";
    private const string ConfigurationMetadataName = "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<T>";
    private const string MigrationMetadataName = "Microsoft.EntityFrameworkCore.Migrations.Migration";
    private const string ModelSnapshotMetadataName = "Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot";
    private const string DbContextAttributeMetadataName = "Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute";

    public static IReadOnlyList<TopologySource> LoadProductionSources(string repoRoot)
    {
        return Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .Where(path => !IsTestProject(path))
            .SelectMany(project => Directory.EnumerateFiles(Path.GetDirectoryName(project)!, "*.cs", SearchOption.AllDirectories))
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new TopologySource(
                ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, path)),
                File.ReadAllText(path)))
            .ToList();
    }

    public static PersistenceTopologyAnalysis Analyze(IEnumerable<TopologySource> sourceFiles)
    {
        _ = typeof(DbContext);
        var sources = sourceFiles.ToList();
        var trees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Content, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest), source.Path))
            .ToList();
        var compilation = ArchitectureTestHelpers.CreateCompilation(trees);
        var dbContexts = new List<DbContextTopologyDeclaration>();
        var dbSets = new List<DbSetTopologyDeclaration>();
        var configurations = new List<EntityTypeConfigurationTopologyDeclaration>();
        var registrations = new List<RegistrarTopologyDeclaration>();
        var migrationTypes = new List<MigrationTypeTopologyDeclaration>();
        var migrationContexts = new List<MigrationContextTopologyDeclaration>();
        var ensureCreated = new List<EnsureCreatedTopologyViolation>();
        var schemaSplits = new List<SchemaSplitTopologyViolation>();

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();
            var sourcePath = ArchitectureTestHelpers.NormalizePath(tree.FilePath);

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol || symbol.IsAbstract)
                {
                    continue;
                }

                if (IsDbContextDeclaration(symbol, declaration))
                {
                    dbContexts.Add(new DbContextTopologyDeclaration(symbol.Name, sourcePath));
                }

                var configuredEntity = GetConfiguredEntity(symbol) ?? GetConfiguredEntity(declaration);
                if (configuredEntity != null)
                {
                    configurations.Add(new EntityTypeConfigurationTopologyDeclaration(
                        configuredEntity,
                        symbol.Name,
                        sourcePath));
                }

                var isSnapshot = Inherits(symbol, ModelSnapshotMetadataName) || HasBaseType(declaration, "ModelSnapshot");
                if (Inherits(symbol, MigrationMetadataName) || isSnapshot || HasBaseType(declaration, "Migration"))
                {
                    migrationTypes.Add(new MigrationTypeTopologyDeclaration(
                        GetMigrationRoot(sourcePath),
                        symbol.Name,
                        isSnapshot));
                }
            }

            foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var entityType = GetDbSetEntity(model, property);
                if (entityType == null || property.Parent is not ClassDeclarationSyntax containingDeclaration ||
                    model.GetDeclaredSymbol(containingDeclaration) is not INamedTypeSymbol containingType ||
                    !IsDbContextDeclaration(containingType, containingDeclaration))
                {
                    continue;
                }

                dbSets.Add(new DbSetTopologyDeclaration(
                    containingType.Name,
                    entityType,
                    sourcePath));
            }

            if (sourcePath.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
                {
                    if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not TypeOfExpressionSyntax typeOf ||
                        !IsDbContextAttribute(model, attribute))
                    {
                        continue;
                    }

                    migrationContexts.Add(new MigrationContextTopologyDeclaration(GetMigrationRoot(sourcePath), GetSimpleName(typeOf.Type)));
                }
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (HasExplicitSchema(model, invocation))
                {
                    schemaSplits.Add(new SchemaSplitTopologyViolation(
                        sourcePath,
                        invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        invocation.ToString()));
                }

                if (!IsEnsureCreated(model, invocation) || IsNonRelationalOnly(model, invocation))
                {
                    continue;
                }

                ensureCreated.Add(new EnsureCreatedTopologyViolation(
                    sourcePath,
                    invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    invocation.ToString()));
            }
        }

        var configurationEntities = configurations
            .GroupBy(configuration => configuration.ConfigurationType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single().EntityType, StringComparer.Ordinal);
        foreach (var tree in trees.Where(tree => Path.GetFileName(tree.FilePath).Equals("AppDbContextEntityTypeConfigurationRegistrar.cs", StringComparison.Ordinal)))
        {
            var root = tree.GetCompilationUnitRoot();
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var configurationType = GetSimpleName(creation.Type);
                if (configurationEntities.TryGetValue(configurationType, out var entityType))
                {
                    registrations.Add(new RegistrarTopologyDeclaration(entityType, configurationType, ArchitectureTestHelpers.NormalizePath(tree.FilePath)));
                }
            }
        }

        var migrationStreams = migrationTypes
            .GroupBy(type => type.Root, StringComparer.Ordinal)
            .Select(group => new MigrationStreamTopologyDeclaration(
                group.Key,
                group.Select(type => type.TypeName).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                group.Where(type => type.IsSnapshot).Select(type => type.TypeName).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                migrationContexts.Where(context => context.Root == group.Key).Select(context => context.ContextType).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList()))
            .OrderBy(stream => stream.Root, StringComparer.Ordinal)
            .ToList();

        return new PersistenceTopologyAnalysis(dbContexts, dbSets, configurations, registrations, migrationStreams, ensureCreated, schemaSplits);
    }

    public static void EnsureNoPendingModelChanges(bool hasPendingModelChanges)
    {
        if (hasPendingModelChanges)
        {
            throw new InvalidOperationException("Npgsql runtime model differs from AppDbContextModelSnapshot.");
        }
    }

    private static bool IsTestProject(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var declaresTestSdk = document.Descendants().Any(element =>
            element.Name.LocalName == "PackageReference" &&
            element.Attribute("Include")?.Value == "Microsoft.NET.Test.Sdk");

        return projectName.EndsWith("Tests", StringComparison.Ordinal) ||
               projectName.EndsWith("TestUtils", StringComparison.Ordinal) ||
               declaresTestSdk;
    }

    private static bool Inherits(INamedTypeSymbol symbol, string metadataName)
    {
        for (var current = symbol.BaseType; current != null; current = current.BaseType)
        {
            if (IsNamed(current, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDbContextDeclaration(INamedTypeSymbol symbol, ClassDeclarationSyntax declaration)
    {
        return Inherits(symbol, DbContextMetadataName) ||
               declaration.BaseList?.Types.OfType<SimpleBaseTypeSyntax>().Any(baseType =>
                   baseType.Type.ToString() is "DbContext" or "Microsoft.EntityFrameworkCore.DbContext") == true;
    }

    private static string? GetConfiguredEntity(INamedTypeSymbol type)
    {
        var configuration = type.AllInterfaces.SingleOrDefault(@interface => IsNamed(@interface, ConfigurationMetadataName));
        return configuration?.TypeArguments[0].Name;
    }

    private static string? GetConfiguredEntity(ClassDeclarationSyntax declaration)
    {
        return declaration.BaseList?.Types.Select(baseType => baseType.Type).OfType<GenericNameSyntax>()
            .Where(type => type.Identifier.ValueText == "IEntityTypeConfiguration")
            .Select(type => GetSimpleName(type.TypeArgumentList.Arguments[0]))
            .SingleOrDefault();
    }

    private static string? GetDbSetEntity(SemanticModel model, PropertyDeclarationSyntax property)
    {
        if (model.GetDeclaredSymbol(property) is IPropertySymbol { Type: INamedTypeSymbol propertyType } && IsNamed(propertyType, DbSetMetadataName))
        {
            return propertyType.TypeArguments[0].Name;
        }

        return property.Type is GenericNameSyntax { Identifier.ValueText: "DbSet" } dbSet
            ? GetSimpleName(dbSet.TypeArgumentList.Arguments[0])
            : null;
    }

    private static bool HasBaseType(ClassDeclarationSyntax declaration, string typeName)
    {
        return declaration.BaseList?.Types.Any(baseType => GetSimpleName(baseType.Type) == typeName) == true;
    }

    private static bool IsDbContextAttribute(SemanticModel model, AttributeSyntax attribute)
    {
        return model.GetSymbolInfo(attribute).Symbol is IMethodSymbol { ContainingType: { } type } &&
               IsNamed(type, DbContextAttributeMetadataName) ||
               attribute.Name.ToString() is "DbContext" or "DbContextAttribute";
    }

    private static string GetSimpleName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
            AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => type.ToString()
        };
    }

    private static bool IsNamed(INamedTypeSymbol type, string metadataName)
    {
        var separator = metadataName.LastIndexOf('.');
        var expectedNamespace = metadataName[..separator];
        var expectedName = metadataName[(separator + 1)..].Split('<')[0];
        return type.OriginalDefinition.ContainingNamespace.ToDisplayString() == expectedNamespace &&
               type.OriginalDefinition.Name == expectedName;
    }

    private static string GetMigrationRoot(string sourcePath)
    {
        const string marker = "/Migrations/";
        var migrationIndex = sourcePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return migrationIndex < 0 ? sourcePath : sourcePath[..migrationIndex] + "/Migrations";
    }

    private static bool IsEnsureCreated(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
               method.Name is "EnsureCreated" or "EnsureCreatedAsync" &&
               model.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString() ==
                   "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade";
    }

    private static bool HasExplicitSchema(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.Name is not ("HasDefaultSchema" or "MigrationsHistoryTable" or "ToTable"))
        {
            return false;
        }

        var schemaArgument = invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
            argument.NameColon?.Name.Identifier.ValueText == "schema");
        if (schemaArgument == null)
        {
            var schemaIndex = method.Name == "HasDefaultSchema" ? 0 : 1;
            schemaArgument = invocation.ArgumentList.Arguments.ElementAtOrDefault(schemaIndex);
        }

        return schemaArgument != null && !IsNullExpression(schemaArgument.Expression);
    }

    private static bool IsNullExpression(ExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.NullLiteralExpression) ||
               expression is CastExpressionSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression };
    }

    private static bool IsNonRelationalOnly(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        return invocation.Ancestors().OfType<IfStatementSyntax>().Any(@if =>
            @if.Condition is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation &&
            negation.Operand is InvocationExpressionSyntax relationalCheck &&
            model.GetSymbolInfo(relationalCheck).Symbol is IMethodSymbol method &&
            method.Name == "IsRelational");
    }
}

internal sealed record TopologySource(string Path, string Content);

internal sealed record PersistenceTopologyAnalysis(
    IReadOnlyList<DbContextTopologyDeclaration> DbContexts,
    IReadOnlyList<DbSetTopologyDeclaration> DbSets,
    IReadOnlyList<EntityTypeConfigurationTopologyDeclaration> Configurations,
    IReadOnlyList<RegistrarTopologyDeclaration> RegistrarEntries,
    IReadOnlyList<MigrationStreamTopologyDeclaration> MigrationStreams,
    IReadOnlyList<EnsureCreatedTopologyViolation> EnsureCreatedViolations,
    IReadOnlyList<SchemaSplitTopologyViolation> SchemaSplitViolations);

internal sealed record DbContextTopologyDeclaration(string TypeName, string SourcePath);
internal sealed record DbSetTopologyDeclaration(string ContextType, string EntityType, string SourcePath);
internal sealed record EntityTypeConfigurationTopologyDeclaration(string EntityType, string ConfigurationType, string SourcePath);
internal sealed record RegistrarTopologyDeclaration(string EntityType, string ConfigurationType, string SourcePath);
internal sealed record MigrationStreamTopologyDeclaration(string Root, IReadOnlyList<string> TypeNames, IReadOnlyList<string> SnapshotTypeNames, IReadOnlyList<string> ContextTypeNames);
internal sealed record EnsureCreatedTopologyViolation(string SourcePath, int Line, string Invocation);
internal sealed record MigrationTypeTopologyDeclaration(string Root, string TypeName, bool IsSnapshot);
internal sealed record MigrationContextTopologyDeclaration(string Root, string ContextType);
internal sealed record SchemaSplitTopologyViolation(string SourcePath, int Line, string Invocation);
