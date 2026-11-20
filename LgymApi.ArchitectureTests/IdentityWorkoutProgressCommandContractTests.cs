using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TrainingCompletedCommand = LgymApi.Application.WorkoutProgress.Contracts.BackgroundCommands.TrainingCompletedCommand;
using UserRegisteredCommand = LgymApi.Application.Identity.Contracts.BackgroundCommands.UserRegisteredCommand;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class IdentityWorkoutProgressCommandContractTests
{
    [Test]
    public void Commands_HaveExactApplicationOwnedPublicSurfaceAndDefaults()
    {
        AssertCommandContract<UserRegisteredCommand>(
            "LgymApi.Application.Identity.Contracts.BackgroundCommands",
            "LgymApi.Identity",
            ("UserId", typeof(Id<User>)));
        AssertCommandContract<TrainingCompletedCommand>(
            "LgymApi.Application.WorkoutProgress.Contracts.BackgroundCommands",
            "LgymApi.Application",
            ("UserId", typeof(Id<User>)),
            ("TrainingId", typeof(Id<Training>)));

        Assert.Multiple(() =>
        {
            Assert.That(new UserRegisteredCommand().UserId, Is.EqualTo(default(Id<User>)));
            Assert.That(new TrainingCompletedCommand().UserId, Is.EqualTo(default(Id<User>)));
            Assert.That(new TrainingCompletedCommand().TrainingId, Is.EqualTo(default(Id<Training>)));
        });
    }

    [Test]
    public void Commands_SerializeToExactLegacyCompatibleGoldenJson()
    {
        var userId = ParseId<User>("00000000-0000-0000-0000-000000000001");
        var trainingId = ParseId<Training>("00000000-0000-0000-0000-000000000002");
        var userRegistered = new UserRegisteredCommand { UserId = userId };
        var trainingCompleted = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        var userRegisteredJson = JsonSerializer.Serialize(userRegistered, SharedSerializationOptions.Current);
        var trainingCompletedJson = JsonSerializer.Serialize(trainingCompleted, SharedSerializationOptions.Current);

        Assert.Multiple(() =>
        {
            Assert.That(userRegisteredJson, Is.EqualTo("{\"userId\":\"00000000-0000-0000-0000-000000000001\"}"));
            Assert.That(trainingCompletedJson, Is.EqualTo("{\"userId\":\"00000000-0000-0000-0000-000000000001\",\"trainingId\":\"00000000-0000-0000-0000-000000000002\"}"));
        });
    }

    private static void AssertCommandContract<TCommand>(
        string expectedNamespace,
        string expectedAssembly,
        params (string Name, Type Type)[] expectedProperties)
    {
        var commandType = typeof(TCommand);
        var properties = commandType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.MetadataToken)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(commandType.Assembly.GetName().Name, Is.EqualTo(expectedAssembly));
            Assert.That(commandType.Namespace, Is.EqualTo(expectedNamespace));
            Assert.That(commandType.IsPublic, Is.True);
            Assert.That(commandType.IsSealed, Is.True);
            Assert.That(commandType.IsClass, Is.True);
            Assert.That(commandType.IsGenericType, Is.False);
            Assert.That(commandType.GetInterfaces(), Is.EqualTo(new[] { typeof(IActionCommand) }));
            Assert.That(properties.Select(property => (property.Name, property.PropertyType)), Is.EqualTo(expectedProperties));
            Assert.That((properties).All(property =>
                property.GetMethod != null
                && property.GetMethod.IsPublic
                && property.SetMethod != null
                && property.SetMethod.IsPublic
                && property.SetMethod.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .Contains(typeof(IsExternalInit))), Is.True);
        });
    }

    private static Id<TEntity> ParseId<TEntity>(string value)
        where TEntity : class
    {
        Assert.That(Id<TEntity>.TryParse(value, out var id), Is.True);
        return id;
    }
}
