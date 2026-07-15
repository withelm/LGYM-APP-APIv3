using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CrossModuleEntityLeakageGuardTests
{
    private const string GuardId = "CrossModuleEntityLeakage";

    private static readonly IReadOnlyDictionary<string, string> EntityOwnerByMetadataName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["LgymApi.Domain.Entities.User"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.Role"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.EloRegistry"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.UserSession"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.UserExternalLogin"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.UserTutorialProgress"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.UserTutorialStepProgress"] = "Identity & Accounts",
        ["LgymApi.Domain.Entities.InAppNotification"] = "Notifications",
        ["LgymApi.Domain.Entities.PushInstallation"] = "Notifications",
        ["LgymApi.Domain.Entities.PushNotificationMessage"] = "Notifications",
        ["LgymApi.Domain.Entities.ReportTemplate"] = "Reporting",
        ["LgymApi.Domain.Entities.ReportTemplateField"] = "Reporting",
        ["LgymApi.Domain.Entities.ReportRequest"] = "Reporting",
        ["LgymApi.Domain.Entities.ReportSubmission"] = "Reporting",
        ["LgymApi.Domain.Entities.RecurringReportAssignment"] = "Reporting",
        ["LgymApi.Domain.Entities.Photo"] = "Reporting",
        ["LgymApi.Domain.Entities.PhotoUploadSession"] = "Reporting",
        ["LgymApi.Domain.Entities.Plan"] = "Training Planning",
        ["LgymApi.Domain.Entities.PlanDay"] = "Training Planning",
        ["LgymApi.Domain.Entities.PlanDayExercise"] = "Training Planning",
        ["LgymApi.Domain.Entities.Training"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.Gym"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.Exercise"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.ExerciseTranslation"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.ExerciseScore"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.TrainingExerciseScore"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.MainRecord"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.Measurement"] = "Workout & Progress",
        ["LgymApi.Domain.Entities.TrainerInvitation"] = "Coaching",
        ["LgymApi.Domain.Entities.TrainerTraineeLink"] = "Coaching",
        ["LgymApi.Domain.Entities.TraineeNote"] = "Coaching",
        ["LgymApi.Domain.Entities.TraineeNoteHistory"] = "Coaching",
        ["LgymApi.Domain.Entities.DietPlan"] = "Nutrition",
        ["LgymApi.Domain.Entities.DietMeal"] = "Nutrition",
        ["LgymApi.Domain.Entities.DietPlanHistory"] = "Nutrition",
        ["LgymApi.Domain.Entities.SupplementPlan"] = "Nutrition",
        ["LgymApi.Domain.Entities.SupplementPlanItem"] = "Nutrition",
        ["LgymApi.Domain.Entities.SupplementIntakeLog"] = "Nutrition"
    };

    private static readonly IReadOnlyDictionary<string, string> RepositoryOwnerByMetadataName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["LgymApi.Application.Repositories.IUserRepository"] = "Identity & Accounts",
        ["LgymApi.Application.Repositories.IRoleRepository"] = "Identity & Accounts",
        ["LgymApi.Application.Repositories.IEloRegistryRepository"] = "Identity & Accounts",
        ["LgymApi.Application.Repositories.IInAppNotificationRepository"] = "Notifications",
        ["LgymApi.Application.Repositories.IPushInstallationRepository"] = "Notifications",
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
        ["LgymApi.Application.Repositories.IMainRecordRepository"] = "Coaching",
        ["LgymApi.Application.Repositories.IMeasurementRepository"] = "Workout & Progress",
        ["LgymApi.Application.Repositories.ITrainerRelationshipRepository"] = "Coaching",
        ["LgymApi.Application.Repositories.ITraineeNoteRepository"] = "Coaching",
        ["LgymApi.Application.Repositories.IDietPlanRepository"] = "Nutrition",
        ["LgymApi.Application.Repositories.ISupplementationRepository"] = "Nutrition"
    };

    [Test]
    public void Application_Modules_Should_Not_Use_Other_Modules_Domain_Entities_Or_Repositories_Directly()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
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

        var violations = observedViolations.Values.OrderBy(v => v.IdentityKey, StringComparer.Ordinal).ToList();

        Assert.Multiple(() =>
        {
            ArchitectureTestHelpers.AssertNoUnexpectedModuleBoundaryViolations(GuardId, violations);

            Assert.That(
                violations.Any(v => v.TargetSymbolOrPath.Contains("Features.", StringComparison.Ordinal)),
                Is.False,
                "Cross-module leakage guard must stay focused on direct entity/repository usage and must not block published contracts/read models/events.");
        });
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
        var metadataName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal);

        if (EntityOwnerByMetadataName.TryGetValue(metadataName, out ownerModule!))
        {
            return true;
        }

        if (RepositoryOwnerByMetadataName.TryGetValue(metadataName, out ownerModule!))
        {
            return true;
        }

        ownerModule = string.Empty;
        return false;
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
                || path.StartsWith("LgymApi.Application/EloRegistry/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/ExternalAuth/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/Tutorial/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/PasswordReset/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/AdminManagement/", StringComparison.OrdinalIgnoreCase)
                => "Identity & Accounts",
            var path when path.StartsWith("LgymApi.Application/Notifications/", StringComparison.OrdinalIgnoreCase)
                => "Notifications",
            var path when path.StartsWith("LgymApi.Application/Features/Reporting/", StringComparison.OrdinalIgnoreCase)
                => "Reporting",
            var path when path.StartsWith("LgymApi.Application/Plan/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/PlanDay/", StringComparison.OrdinalIgnoreCase)
                => "Training Planning",
            var path when path.StartsWith("LgymApi.Application/Training/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Exercise/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/ExerciseScores/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Gym/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Measurements/", StringComparison.OrdinalIgnoreCase)
                => "Workout & Progress",
            var path when path.StartsWith("LgymApi.Application/TrainerRelationships/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/MainRecords/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/TraineeNotes/", StringComparison.OrdinalIgnoreCase)
                => "Coaching",
            var path when path.StartsWith("LgymApi.Application/Features/DietPlans/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("LgymApi.Application/Features/Supplementation/", StringComparison.OrdinalIgnoreCase)
                => "Nutrition",
            _ => null
        };
    }
}
