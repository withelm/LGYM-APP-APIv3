using LgymApi.BackgroundWorker.Common;

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
}
