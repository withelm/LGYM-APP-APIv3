using FluentAssertions;
using LgymApi.BackgroundWorker.Common;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests for CommandDescriptor policy: exact-type matching, serialization round-trip,
/// and rejection of polymorphic matching (no IsAssignableFrom).
/// </summary>
[TestFixture]
public sealed class CommandDescriptorTests
{
     [Test]
     public void Constructor_WithValidType_StoresTypeFullName()
     {
         // Arrange
         var testType = typeof(string);

         // Act
         var descriptor = new CommandDescriptor(testType);

         // Assert
         descriptor.TypeFullName.Should().Be(testType.FullName);
         descriptor.CommandType.Should().Be(testType);
     }

     [Test]
     public void Constructor_WithNullType_ThrowsArgumentNullException()
     {
         // Act & Assert
         var action = () => new CommandDescriptor(null!);
         var ex = action.Should().Throw<ArgumentNullException>().Which;
         ex.ParamName.Should().Be("commandType");
     }


    [Test]
    public void FromPersistedType_WithValidTypeFullName_ReturnsDescriptor()
    {
        // Arrange
        var typeFullName = "System.String";

        // Act
        var descriptor = CommandDescriptor.FromPersistedType(typeFullName);

        // Assert
        descriptor.TypeFullName.Should().Be(typeFullName);
    }

    [Test]
    public void FromPersistedType_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        var action1 = () => CommandDescriptor.FromPersistedType(null!);
        var action2 = () => CommandDescriptor.FromPersistedType("");
        var action3 = () => CommandDescriptor.FromPersistedType("   ");
        
        action1.Should().Throw<ArgumentException>();
        action2.Should().Throw<ArgumentException>();
        action3.Should().Throw<ArgumentException>();
    }

    [Test]
    public void IsExactTypeMatch_WithSameType_ReturnsTrue()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(string));

        // Act
        var result = descriptor1.IsExactTypeMatch(descriptor2);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsExactTypeMatch_WithDifferentTypes_ReturnsFalse()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(int));

        // Act
        var result = descriptor1.IsExactTypeMatch(descriptor2);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsExactTypeMatch_WithNull_ReturnsFalse()
    {
        // Arrange
        var descriptor = new CommandDescriptor(typeof(string));

        // Act
        var result = descriptor.IsExactTypeMatch(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsExactTypeMatch_WithBaseAndDerivedType_ReturnsFalse_NoPolymorphicMatching()
    {
        // Arrange
        var baseDescriptor = new CommandDescriptor(typeof(object));
        var derivedDescriptor = new CommandDescriptor(typeof(string));

        // Act
        var result = baseDescriptor.IsExactTypeMatch(derivedDescriptor);

        // Assert - Ensure no polymorphic matching (no IsAssignableFrom behavior)
        result.Should().BeFalse();
    }

    [Test]
    public void ResolveCommandType_WithValidTypeFullName_ReturnsType()
    {
        // Arrange
        var typeFullName = typeof(string).FullName!;

        // Act
        var resolvedType = CommandDescriptor.ResolveCommandType(typeFullName);

        // Assert
        resolvedType.Should().Be(typeof(string));
    }

    [Test]
    public void ResolveCommandType_WithInvalidTypeFullName_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidTypeFullName = "NonExistent.Type.That.Does.Not.Exist";

        // Act & Assert
        var action = () => CommandDescriptor.ResolveCommandType(invalidTypeFullName);
        var ex = action.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Cannot resolve command type");
        ex.Message.Should().Contain(invalidTypeFullName);
    }

    [Test]
    public void ResolveCommandType_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        var action1 = () => CommandDescriptor.ResolveCommandType(null!);
        var action2 = () => CommandDescriptor.ResolveCommandType("");
        var action3 = () => CommandDescriptor.ResolveCommandType("   ");
        
        action1.Should().Throw<ArgumentException>();
        action2.Should().Throw<ArgumentException>();
        action3.Should().Throw<ArgumentException>();
    }

    [Test]
    public void RoundTrip_Serialization_PreservesTypeIdentity()
    {
        // Arrange
        var originalType = typeof(List<string>);
        var originalDescriptor = new CommandDescriptor(originalType);

        // Act - persist type full name, then recreate descriptor
        var persistedTypeFullName = originalDescriptor.TypeFullName;
        var restoredDescriptor = CommandDescriptor.FromPersistedType(persistedTypeFullName);
        var resolvedType = CommandDescriptor.ResolveCommandType(restoredDescriptor.TypeFullName);

        // Assert
        resolvedType.Should().Be(originalType);
        restoredDescriptor.IsExactTypeMatch(originalDescriptor).Should().BeTrue();
    }

    [Test]
    public void Equals_WithSameType_ReturnsTrue()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(string));

        // Act & Assert
        descriptor1.Equals(descriptor2).Should().BeTrue();
        (descriptor1 == descriptor2).Should().BeFalse(); // No operator overload, so reference equality
    }

    [Test]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(int));

        // Act & Assert
        descriptor1.Equals(descriptor2).Should().BeFalse();
    }

    [Test]
    public void GetHashCode_WithSameType_ReturnsSameHash()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(string));

        // Act & Assert
        descriptor1.GetHashCode().Should().Be(descriptor2.GetHashCode());
    }

    [Test]
    public void ToString_ReturnsTypeFullName()
    {
        // Arrange
        var descriptor = new CommandDescriptor(typeof(string));

        // Act
        var result = descriptor.ToString();

        // Assert
        result.Should().Be(typeof(string).FullName);
    }

    [Test]
    public void ExactTypeMatching_DoesNotApplyIsAssignableFrom_DespiteInheritance()
    {
        // Arrange
        var baseDescriptor = new CommandDescriptor(typeof(ArgumentException));
        var derivedDescriptor = new CommandDescriptor(typeof(ArgumentNullException));

        // Note: ArgumentNullException IS AssignableFrom to ArgumentException,
        // but CommandDescriptor must NOT use that rule.

        // Act
        var result = baseDescriptor.IsExactTypeMatch(derivedDescriptor);

        // Assert - Confirm exact-type-only matching
        result.Should().BeFalse();

        // Verify that base class assignment would normally work
        typeof(ArgumentException).IsAssignableFrom(typeof(ArgumentNullException)).Should().BeTrue();
    }
}
