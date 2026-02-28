using LgymApi.BackgroundWorker.Common;

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
        Assert.That(descriptor.TypeFullName, Is.EqualTo(testType.FullName));
        Assert.That(descriptor.CommandType, Is.EqualTo(testType));
    }

    [Test]
    public void Constructor_WithNullType_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CommandDescriptor(null!));
        Assert.That(ex.ParamName, Is.EqualTo("commandType"));
    }



    [Test]
    public void FromPersistedType_WithValidTypeFullName_ReturnsDescriptor()
    {
        // Arrange
        var typeFullName = "System.String";

        // Act
        var descriptor = CommandDescriptor.FromPersistedType(typeFullName);

        // Assert
        Assert.That(descriptor.TypeFullName, Is.EqualTo(typeFullName));
    }

    [Test]
    public void FromPersistedType_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => CommandDescriptor.FromPersistedType(null!));
        Assert.Throws<ArgumentException>(() => CommandDescriptor.FromPersistedType(""));
        Assert.Throws<ArgumentException>(() => CommandDescriptor.FromPersistedType("   "));
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
        Assert.That(result, Is.True);
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
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsExactTypeMatch_WithNull_ReturnsFalse()
    {
        // Arrange
        var descriptor = new CommandDescriptor(typeof(string));

        // Act
        var result = descriptor.IsExactTypeMatch(null!);

        // Assert
        Assert.That(result, Is.False);
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
        Assert.That(result, Is.False);
    }

    [Test]
    public void ResolveCommandType_WithValidTypeFullName_ReturnsType()
    {
        // Arrange
        var typeFullName = typeof(string).FullName!;

        // Act
        var resolvedType = CommandDescriptor.ResolveCommandType(typeFullName);

        // Assert
        Assert.That(resolvedType, Is.EqualTo(typeof(string)));
    }

    [Test]
    public void ResolveCommandType_WithInvalidTypeFullName_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidTypeFullName = "NonExistent.Type.That.Does.Not.Exist";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => CommandDescriptor.ResolveCommandType(invalidTypeFullName));
        Assert.That(ex.Message, Contains.Substring("Cannot resolve command type"));
        Assert.That(ex.Message, Contains.Substring(invalidTypeFullName));
    }

    [Test]
    public void ResolveCommandType_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => CommandDescriptor.ResolveCommandType(null!));
        Assert.Throws<ArgumentException>(() => CommandDescriptor.ResolveCommandType(""));
        Assert.Throws<ArgumentException>(() => CommandDescriptor.ResolveCommandType("   "));
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
        Assert.That(resolvedType, Is.EqualTo(originalType));
        Assert.That(restoredDescriptor.IsExactTypeMatch(originalDescriptor), Is.True);
    }

    [Test]
    public void Equals_WithSameType_ReturnsTrue()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(string));

        // Act & Assert
        Assert.That(descriptor1.Equals(descriptor2), Is.True);
        Assert.That(descriptor1 == descriptor2, Is.False); // No operator overload, so reference equality
    }

    [Test]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(int));

        // Act & Assert
        Assert.That(descriptor1.Equals(descriptor2), Is.False);
    }

    [Test]
    public void GetHashCode_WithSameType_ReturnsSameHash()
    {
        // Arrange
        var descriptor1 = new CommandDescriptor(typeof(string));
        var descriptor2 = new CommandDescriptor(typeof(string));

        // Act & Assert
        Assert.That(descriptor1.GetHashCode(), Is.EqualTo(descriptor2.GetHashCode()));
    }

    [Test]
    public void ToString_ReturnsTypeFullName()
    {
        // Arrange
        var descriptor = new CommandDescriptor(typeof(string));

        // Act
        var result = descriptor.ToString();

        // Assert
        Assert.That(result, Is.EqualTo(typeof(string).FullName));
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
        Assert.That(result, Is.False,
            "Descriptor matching must enforce exact-type equality, not polymorphic matching");

        // Verify that base class assignment would normally work
        Assert.That(typeof(ArgumentException).IsAssignableFrom(typeof(ArgumentNullException)), Is.True,
            "Sanity check: ArgumentNullException IS assignable to ArgumentException");
    }
}
