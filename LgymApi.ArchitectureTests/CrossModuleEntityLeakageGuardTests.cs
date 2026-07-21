using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LgymApi.Domain.Entities;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CrossModuleEntityLeakageGuardTests
{
    private const string GuardId = "CrossModuleEntityLeakage";

    private static readonly IReadOnlyDictionary<string, string> RepositoryOwnerByMetadataName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["LgymApi.Application.Repositories.IUserRepository"] = "Identity & Accounts",
        ["LgymApi.Application.Repositories.IRoleRepository"] = "Identity & Accounts",
        ["LgymApi.Application.Repositories.IEloRegistryRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.IInAppNotificationRepository"] = "Notifications",
        ["LgymApi.Application.Notifications.Repositories.IPushInstallationRepository"] = "Notifications",
        ["LgymApi.Application.Repositories.IPushNotificationMessageRepository"] = "Notifications",
        ["LgymApi.Application.Repositories.IReportingRepository"] = "Reporting",
        ["LgymApi.Application.Repositories.IRecurringReportAssignmentRepository"] = "Reporting",
        ["LgymApi.Application.Repositories.IPlanRepository"] = "Training Planning",
        ["LgymApi.Application.Repositories.IPlanDayRepository"] = "Training Planning",
        ["LgymApi.Application.Repositories.IPlanDayExerciseRepository"] = "Training Planning",
        ["LgymApi.Application.Repositories.IGymRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.ITrainingRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.IExerciseRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.IExerciseScoreRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.ITrainingExerciseScoreRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.IMainRecordRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.IMeasurementRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.ITrainerRelationshipRepository"] = "Coaching",
        ["LgymApi.Application.Repositories.ITraineeNoteRepository"] = "Coaching",
        ["LgymApi.Application.Repositories.IDietPlanRepository"] = "Nutrition",
        ["LgymApi.Application.Repositories.ISupplementationRepository"] = "Nutrition"
    };

    [TestCase("LgymApi.Application.Repositories.IEloRegistryRepository", "Workout & Progress")]
    [TestCase("LgymApi.Application.Repositories.IMainRecordRepository", "Workout & Progress")]
    public void Moved_Repositories_Should_Use_Canonical_Owners(string metadataName, string expectedOwner)
    {
        Assert.That(RepositoryOwnerByMetadataName[metadataName], Is.EqualTo(expectedOwner));
    }

    [TestCase("LgymApi.Application/EloRegistry/EloRegistryService.cs", "Workout & Progress")]
    [TestCase("LgymApi.Application/MainRecords/MainRecordsService.cs", "Workout & Progress")]
    [TestCase("LgymApi.Application/WorkoutProgress/Contracts/WorkoutProgressContract.cs", "Workout & Progress")]
    public void Moved_Application_Paths_Should_Use_Canonical_Owners(string path, string expectedOwner)
    {
        Assert.That(TryGetApplicationModuleName(path), Is.EqualTo(expectedOwner));
    }

    [Test]
    public void Direct_Foreign_Entity_Exposure_Should_Fail_While_Typed_Ids_Are_Allowed()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var applicationTree = CSharpSyntaxTree.ParseText("""
            using LgymApi.Domain.ValueObjects;
            using UserEntity = LgymApi.Domain.Entities.User;

            namespace LgymApi.Application.Features.Reporting;

            public sealed class ForeignEntityExposure
            {
                public UserEntity ForeignUser { get; init; }
                public Id<UserEntity> ForeignUserId { get; init; }
                public Id<LgymApi.Domain.Entities.User> FullyQualifiedForeignUserId { get; init; }

                public UserEntity ReturnForeignUser() => default;

                public void AcceptForeignUser(UserEntity user)
                {
                }
            }
            """, path: Path.Combine(repoRoot, "LgymApi.Application", "Features", "Reporting", "ForeignEntityExposure.cs"));
        var compilation = CSharpCompilation.Create(
            "CrossModuleEntityLeakageFixture",
            [applicationTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(User).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var expectedOwner = PersistedEntityOwnershipCatalog.Entries
            .Single(entry => entry.EntityType == typeof(User))
            .Owner;
        var violations = CollectViolations(
            compilation,
            [applicationTree],
            repoRoot);

        TestContext.Progress.WriteLine(
            $"Typed fixture source 'ForeignUserId' must not expose '{typeof(User).FullName}'; expected direct owner: {expectedOwner}.");

        Assert.Multiple(() =>
        {
            Assert.That(violations.Select(violation => violation.SourceSymbolOrPath), Has.Some.Contains("ForeignUser"));
            Assert.That(violations.Select(violation => violation.SourceSymbolOrPath), Has.Some.Contains("ReturnForeignUser"));
            Assert.That(violations.Select(violation => violation.SourceSymbolOrPath), Has.Some.Contains("AcceptForeignUser"));
            Assert.That(violations.Select(violation => violation.SourceSymbolOrPath), Has.None.Contains("ForeignUserId"));
            Assert.That(violations.Select(violation => violation.SourceSymbolOrPath), Has.None.Contains("FullyQualifiedForeignUserId"));
            Assert.That(violations.All(violation => violation.TargetModule == expectedOwner), Is.True);
        });
    }

    [Test]
    public void Application_Modules_Should_Not_Use_Other_Modules_Domain_Entities_Or_Repositories_Directly()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var violations = CollectViolations(compilation, syntaxTrees, repoRoot);

        Assert.Multiple(() =>
        {
            ArchitectureTestHelpers.AssertNoUnexpectedModuleBoundaryViolations(GuardId, violations);

            Assert.That(
                violations.Any(v => v.TargetSymbolOrPath.Contains("Features.", StringComparison.Ordinal)),
                Is.False,
                "Cross-module leakage guard must stay focused on direct entity/repository usage and must not block published contracts/read models/events.");
        });
    }

    private static IReadOnlyList<ModuleBoundaryObservedViolation> CollectViolations(
        CSharpCompilation compilation,
        IEnumerable<SyntaxTree> syntaxTrees,
        string repoRoot)
    {
        var observedViolations = new Dictionary<string, ModuleBoundaryObservedViolation>(StringComparer.Ordinal);

        foreach (var tree in syntaxTrees)
        {
            var sourceFile = ArchitectureTestHelpers.ClassifyModuleBoundaryFile(tree.FilePath, repoRoot);
            if (sourceFile.IsExcluded)
            {
                continue;
            }

            var sourceModule = TryGetApplicationModuleName(sourceFile.RelativePath);
            if (string.IsNullOrWhiteSpace(sourceModule))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeSyntax in root.DescendantNodes().OfType<TypeSyntax>())
            {
                if (IsTypedEntityIdArgument(typeSyntax, semanticModel)
                    || IsTypedEntityIdAliasDeclaration(typeSyntax, semanticModel, root, sourceFile.RelativePath))
                {
                    continue;
                }

                var symbol = semanticModel.GetTypeInfo(typeSyntax).Type;
                if (symbol == null)
                {
                    continue;
                }

                foreach (var referencedType in EnumerateRelevantNamedTypes(symbol))
                {
                    if (!TryResolveTargetOwner(referencedType, out var targetModule))
                    {
                        continue;
                    }

                    if (string.Equals(sourceModule, targetModule, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var sourceSymbol = GetEnclosingSourceSymbol(semanticModel, typeSyntax) ?? sourceFile.RelativePath;
                    var targetSymbol = referencedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);
                    var violation = new ModuleBoundaryObservedViolation(GuardId, sourceModule, targetModule, sourceSymbol, targetSymbol);
                    observedViolations.TryAdd(violation.IdentityKey, violation);
                }
            }
        }

        return observedViolations.Values.OrderBy(violation => violation.IdentityKey, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateRelevantNamedTypes(ITypeSymbol symbol)
    {
        foreach (var candidate in EnumerateNamedTypes(symbol))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(ITypeSymbol symbol)
    {
        if (symbol is INamedTypeSymbol namedType)
        {
            yield return namedType;

            if (IsTypedEntityId(namedType))
            {
                yield break;
            }

            foreach (var typeArgument in namedType.TypeArguments)
            {
                foreach (var nested in EnumerateNamedTypes(typeArgument))
                {
                    yield return nested;
                }
            }
        }

        if (symbol.NullableAnnotation != NullableAnnotation.None && symbol is INamedTypeSymbol { TypeArguments.Length: 1 } nullableType)
        {
            foreach (var nested in EnumerateNamedTypes(nullableType.TypeArguments[0]))
            {
                yield return nested;
            }
        }

        if (symbol is IArrayTypeSymbol arrayType)
        {
            foreach (var nested in EnumerateNamedTypes(arrayType.ElementType))
            {
                yield return nested;
            }
        }
    }

    private static bool TryResolveTargetOwner(INamedTypeSymbol symbol, out string ownerModule)
    {
        if (ArchitectureTestHelpers.TryGetPersistedEntityOwner(symbol, out ownerModule))
        {
            return true;
        }

        var metadataName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);

        if (RepositoryOwnerByMetadataName.TryGetValue(metadataName, out ownerModule!))
        {
            return true;
        }

        ownerModule = string.Empty;
        return false;
    }

    private static bool IsTypedEntityIdArgument(TypeSyntax typeSyntax, SemanticModel semanticModel)
    {
        return typeSyntax.Ancestors()
            .OfType<TypeArgumentListSyntax>()
            .Any(typeArgumentList =>
                typeArgumentList.Parent is GenericNameSyntax genericName &&
                semanticModel.GetTypeInfo(genericName).Type is INamedTypeSymbol type
                && IsTypedEntityId(type));
    }

    private static bool IsTypedEntityIdAliasDeclaration(
        TypeSyntax typeSyntax,
        SemanticModel semanticModel,
        CompilationUnitSyntax root,
        string relativePath)
    {
        if (!relativePath.StartsWith("LgymApi.Application/TrainingPlanning/Plan/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var usingDirective = typeSyntax.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
        if (usingDirective?.Alias == null || semanticModel.GetDeclaredSymbol(usingDirective) is not IAliasSymbol aliasSymbol)
        {
            return false;
        }

        var aliasUsages = root
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => SymbolEqualityComparer.Default.Equals(semanticModel.GetAliasInfo(identifier), aliasSymbol))
            .ToList();

        return aliasUsages.Count > 0 && aliasUsages.All(aliasUsage => IsTypedEntityIdArgument(aliasUsage, semanticModel));
    }

    private static bool IsTypedEntityId(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::LgymApi.Domain.ValueObjects.Id<TEntity>";
    }

    private static string? GetEnclosingSourceSymbol(SemanticModel semanticModel, SyntaxNode node)
    {
        foreach (var current in node.AncestorsAndSelf())
        {
            ISymbol? symbol = current switch
            {
                MethodDeclarationSyntax method => semanticModel.GetDeclaredSymbol(method),
                ConstructorDeclarationSyntax constructor => semanticModel.GetDeclaredSymbol(constructor),
                PropertyDeclarationSyntax property => semanticModel.GetDeclaredSymbol(property),
                FieldDeclarationSyntax field when field.Declaration.Variables.FirstOrDefault() is { } variable => semanticModel.GetDeclaredSymbol(variable),
                EventDeclarationSyntax @event => semanticModel.GetDeclaredSymbol(@event),
                ClassDeclarationSyntax @class => semanticModel.GetDeclaredSymbol(@class),
                InterfaceDeclarationSyntax @interface => semanticModel.GetDeclaredSymbol(@interface),
                RecordDeclarationSyntax record => semanticModel.GetDeclaredSymbol(record),
                StructDeclarationSyntax @struct => semanticModel.GetDeclaredSymbol(@struct),
                _ => null
            };

            if (symbol != null)
            {
                return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);
            }
        }

        return null;
    }

    private static string? TryGetApplicationModuleName(string relativePath)
    {
        var normalized = ArchitectureTestHelpers.NormalizePath(relativePath);

        return normalized switch
        {
            var path when path.StartsWith("LgymApi.Application/User/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Role/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/ExternalAuth/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/Tutorial/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/PasswordReset/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/AdminManagement/", StringComparison.OrdinalIgnoreCase)
                => "Identity & Accounts",
            var path when path.StartsWith("LgymApi.Application/Notifications/", StringComparison.OrdinalIgnoreCase)
                => "Notifications",
            var path when path.StartsWith("LgymApi.Application/Features/Reporting/", StringComparison.OrdinalIgnoreCase)
                => "Reporting",
            var path when path.StartsWith("LgymApi.Application/TrainingPlanning/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/PlanDay/", StringComparison.OrdinalIgnoreCase)
                => "Training Planning",
            var path when path.StartsWith("LgymApi.Application/WorkoutProgress/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Training/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/EloRegistry/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Exercise/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/ExerciseScores/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Gym/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Measurements/", StringComparison.OrdinalIgnoreCase)
                => "Workout & Progress",
            var path when path.StartsWith("LgymApi.Application/TrainerRelationships/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/TraineeNotes/", StringComparison.OrdinalIgnoreCase)
                => "Coaching",
            var path when path.StartsWith("LgymApi.Application/MainRecords/", StringComparison.OrdinalIgnoreCase)
                => "Workout & Progress",
            var path when path.StartsWith("LgymApi.Application/Features/DietPlans/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/Supplementation/", StringComparison.OrdinalIgnoreCase)
                => "Nutrition",
            _ => null
        };
    }
}
