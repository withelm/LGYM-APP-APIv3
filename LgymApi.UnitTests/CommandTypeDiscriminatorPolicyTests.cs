using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandTypeDiscriminatorPolicyTests
{
    [Test]
    public void GetDiscriminator_UsesTypeFullName()
    {
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(List<string>));

        Assert.That(discriminator, Is.EqualTo(typeof(List<string>).FullName));
    }

    [Test]
    public void IsExactMatch_ReturnsFalse_ForAssignableButDifferentTypes()
    {
        var baseDiscriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(ArgumentException));
        var derivedDiscriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(ArgumentNullException));

        Assert.That(CommandTypeDiscriminatorPolicy.IsExactMatch(baseDiscriminator, derivedDiscriminator), Is.False);
        Assert.That(typeof(ArgumentException).IsAssignableFrom(typeof(ArgumentNullException)), Is.True);
    }

    [Test]
    public void ResolveType_RoundTripsTypeFromDiscriminator()
    {
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(Dictionary<string, int>));

        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);

        Assert.That(resolvedType, Is.EqualTo(typeof(Dictionary<string, int>)));
    }

    [Test]
    public void ResolveType_Throws_ForUnknownDiscriminator()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CommandTypeDiscriminatorPolicy.ResolveType("Fake.Namespace.UnknownCommand"));
    }

    [Test]
    public void IsExactMatch_ReturnsFalse_ForNullOrWhitespace()
    {
        Assert.That(CommandTypeDiscriminatorPolicy.IsExactMatch("System.String", ""), Is.False);
        Assert.That(CommandTypeDiscriminatorPolicy.IsExactMatch("", "System.String"), Is.False);
    }

    #region Known Command Type Resolution Tests

    [Test]
    public void ResolveType_ResolvesUserRegisteredCommand()
    {
        // Arrange - get discriminator from actual command type
        var commandType = typeof(UserRegisteredCommand);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

        // Act - resolve type from discriminator
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);

        // Assert - exact type equality
        Assert.That(resolvedType, Is.EqualTo(commandType));
        Assert.That(resolvedType.FullName, Is.EqualTo("LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand"));
    }

    [Test]
    public void ResolveType_ResolvesTrainingCompletedCommand()
    {
        // Arrange - get discriminator from actual command type
        var commandType = typeof(TrainingCompletedCommand);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

        // Act - resolve type from discriminator
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);

        // Assert - exact type equality
        Assert.That(resolvedType, Is.EqualTo(commandType));
        Assert.That(resolvedType.FullName, Is.EqualTo("LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand"));
    }

    [Test]
    public void ResolveType_ResolvesInvitationCreatedCommand()
    {
        // Arrange - get discriminator from actual command type
        var commandType = typeof(InvitationCreatedCommand);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

        // Act - resolve type from discriminator
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);

        // Assert - exact type equality
        Assert.That(resolvedType, Is.EqualTo(commandType));
        Assert.That(resolvedType.FullName, Is.EqualTo("LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand"));
    }

    [Test]
    public void ResolveType_ResolvesAllKnownCommandsFromPersistedDiscriminators()
    {
        // Arrange - simulate persisted discriminator strings (FullName)
        var persistedDiscriminators = new[]
        {
            "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand",
            "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand",
            "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand"
        };

        var expectedTypes = new[]
        {
            typeof(UserRegisteredCommand),
            typeof(TrainingCompletedCommand),
            typeof(InvitationCreatedCommand)
        };

        // Act & Assert - all persisted discriminators resolve correctly
        for (int i = 0; i < persistedDiscriminators.Length; i++)
        {
            var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminators[i]);
            Assert.That(resolvedType, Is.EqualTo(expectedTypes[i]),
                $"Failed to resolve persisted discriminator: {persistedDiscriminators[i]}");
        }
    }

    [Test]
    public void GetDiscriminator_GeneratesConsistentDiscriminatorsForAllKnownCommands()
    {
        // Arrange
        var commandTypes = new[]
        {
            typeof(UserRegisteredCommand),
            typeof(TrainingCompletedCommand),
            typeof(InvitationCreatedCommand)
        };

        // Act - generate discriminators multiple times
        var firstPassDiscriminators = commandTypes.Select(t => CommandTypeDiscriminatorPolicy.GetDiscriminator(t)).ToList();
        var secondPassDiscriminators = commandTypes.Select(t => CommandTypeDiscriminatorPolicy.GetDiscriminator(t)).ToList();

        // Assert - discriminators are stable and consistent
        Assert.That(secondPassDiscriminators, Is.EqualTo(firstPassDiscriminators));
        Assert.That(firstPassDiscriminators.Distinct().Count(), Is.EqualTo(commandTypes.Length),
            "All command discriminators must be unique");
    }

    #endregion
}
