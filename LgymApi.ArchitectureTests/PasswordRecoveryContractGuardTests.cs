using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class PasswordRecoveryContractGuardTests
{
    private const string ContractsNamespace = "LgymApi.Application.Features.PasswordReset.Contracts";
    private const string RequestTypeName = $"{ContractsNamespace}.PasswordRecoveryEmailRequest";
    private const string SchedulerTypeName = $"{ContractsNamespace}.IPasswordRecoveryEmailScheduler";

    [Test]
    public void PasswordRecoveryEmailRequest_HasExactlySevenOrderedValues()
    {
        var requestType = GetApplicationType(RequestTypeName);
        var assertedValue4 = (requestType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)).ToArray();
        Assert.That(assertedValue4, Has.Exactly(1).Items);
        var constructor = assertedValue4.Single();
        var expectedTypes = new[]
        {
            "LgymApi.Domain.ValueObjects.Id`1[LgymApi.Domain.Entities.User]",
            "LgymApi.Domain.ValueObjects.Id`1[LgymApi.Domain.Entities.PasswordResetToken]",
            typeof(string).FullName!,
            typeof(string).FullName!,
            typeof(string).FullName!,
            typeof(string).FullName!,
            typeof(string).FullName!
        };
        var expectedNames = new[]
        {
            "UserId",
            "TokenId",
            "UserName",
            "RecipientEmail",
            "ResetToken",
            "ResetUrl",
            "CultureName"
        };

        Assert.That(requestType.IsPublic, Is.True);
        Assert.That(requestType.IsSealed, Is.True);
        Assert.That(constructor.GetParameters().Select(parameter => parameter.Name), Is.EqualTo(expectedNames));
        Assert.That(constructor.GetParameters().Select(parameter => parameter.ParameterType.ToString()), Is.EqualTo(expectedTypes));

        var properties = requestType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.That(properties.Select(property => property.Name), Is.EqualTo(expectedNames));
        Assert.That(properties.Select(property => property.PropertyType.ToString()), Is.EqualTo(expectedTypes));
    }

    [Test]
    public void IPasswordRecoveryEmailScheduler_HasOnlyTheExactScheduleAsyncSignature()
    {
        var requestType = GetApplicationType(RequestTypeName);
        var schedulerType = GetApplicationType(SchedulerTypeName);
        var assertedValue5 = (schedulerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToArray();
        Assert.That(assertedValue5, Has.Exactly(1).Items);
        var method = assertedValue5.Single();

        Assert.That(schedulerType.IsPublic, Is.True);
        Assert.That(schedulerType.IsInterface, Is.True);
        Assert.That(schedulerType.IsGenericType, Is.False);
        Assert.That(schedulerType.GetInterfaces(), Is.Empty);
        Assert.That(schedulerType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        Assert.That(schedulerType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);

        Assert.That(method.Name, Is.EqualTo("ScheduleAsync"));
        Assert.That(method.IsGenericMethod, Is.False);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task)));

        var parameters = method.GetParameters();
        Assert.That(parameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "request", "cancellationToken" }));
        Assert.That(parameters.Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { requestType, typeof(CancellationToken) }));
        Assert.That(parameters[0].IsOptional, Is.False);
        Assert.That(parameters[1].IsOptional, Is.True);
        Assert.That(parameters[1].HasDefaultValue, Is.True);
    }

    [Test]
    public void PasswordRecoveryContracts_DoNotExposeForbiddenDependencies()
    {
        var (_, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Identity");
        var contractTrees = syntaxTrees
            .Where(tree => ArchitectureTestHelpers.NormalizePath(tree.FilePath)
                .Contains("/Features/PasswordReset/Contracts/", StringComparison.Ordinal))
            .OrderBy(tree => tree.FilePath, StringComparer.Ordinal)
            .ToArray();

        Assert.That(contractTrees.Select(tree => Path.GetFileName(tree.FilePath)), Is.EqualTo(new[] { "IPasswordRecoveryEmailScheduler.cs", "PasswordRecoveryEmailRequest.cs" }));

        var exposedTypeNames = contractTrees
            .SelectMany(tree => GetReferencedTypeNames(compilation, tree))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That((exposedTypeNames).Any(typeName =>
            typeName.Contains("LgymApi.BackgroundWorker", StringComparison.Ordinal)
            || typeName.Contains("LgymApi.Application.Notifications", StringComparison.Ordinal)
            || typeName.Contains("LgymApi.Infrastructure", StringComparison.Ordinal)
            || typeName.Contains("Hangfire", StringComparison.Ordinal)), Is.False);
    }

    private static IEnumerable<string> GetReferencedTypeNames(Compilation compilation, SyntaxTree tree)
    {
        var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

        foreach (var typeSyntax in tree.GetRoot().DescendantNodes().OfType<TypeSyntax>())
        {
            if (semanticModel.GetTypeInfo(typeSyntax).Type is ITypeSymbol typeSymbol)
            {
                yield return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
    }

    private static Type GetApplicationType(string metadataName)
    {
        var type = typeof(LgymApi.Identity.IdentityModule).Assembly.GetType(metadataName);
        Assert.That(type, Is.Not.Null, $"{metadataName} must be defined by the Identity assembly");
        return type!;
    }
}
