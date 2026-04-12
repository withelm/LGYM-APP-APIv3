using FluentAssertions;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using NUnit.Framework;

[TestFixture]
public sealed class CommandTypeDiscriminatorPolicyTests
{
    [Test]
    public void GetDiscriminator_UsesTypeFullName()
    {
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(List<string>));

        discriminator.Should().Be(typeof(List<string>).FullName);
    }

    [Test]
    public void IsExactMatch_ReturnsFalse_ForAssignableButDifferentTypes()
    {
        var baseDiscriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(ArgumentException));
        var derivedDiscriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(ArgumentNullException));

        CommandTypeDiscriminatorPolicy.IsExactMatch(baseDiscriminator, derivedDiscriminator).Should().BeFalse();
        typeof(ArgumentException).IsAssignableFrom(typeof(ArgumentNullException)).Should().BeTrue();
    }

    [Test]
    public void ResolveType_RoundTripsTypeFromDiscriminator()
    {
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(typeof(Dictionary<string, int>));

        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);

        resolvedType.Should().Be(typeof(Dictionary<string, int>));
    }

    [Test]
    public void ResolveType_Throws_ForUnknownDiscriminator()
    {
        var action = () => CommandTypeDiscriminatorPolicy.ResolveType("Fake.Namespace.UnknownCommand");
        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void IsExactMatch_ReturnsFalse_ForNullOrWhitespace()
    {
        CommandTypeDiscriminatorPolicy.IsExactMatch("System.String", "").Should().BeFalse();
        CommandTypeDiscriminatorPolicy.IsExactMatch("", "System.String").Should().BeFalse();
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
        resolvedType.Should().Be(commandType);
        resolvedType.FullName.Should().Be("LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand");
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
        resolvedType.Should().Be(commandType);
        resolvedType.FullName.Should().Be("LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand");
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
        resolvedType.Should().Be(commandType);
        resolvedType.FullName.Should().Be("LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand");
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
            resolvedType.Should().Be(expectedTypes[i], $"Failed to resolve persisted discriminator: {persistedDiscriminators[i]}");
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
        secondPassDiscriminators.Should().Equal(firstPassDiscriminators);
        firstPassDiscriminators.Distinct().Count().Should().Be(commandTypes.Length, "All command discriminators must be unique");
    }

    #endregion
}
