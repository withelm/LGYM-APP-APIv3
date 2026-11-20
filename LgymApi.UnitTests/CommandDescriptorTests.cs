using LgymApi.BackgroundWorker.Runtime;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandDescriptorTests
{
    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void Constructor_StoresKnownRuntimeTypeAndCanonicalId(LegacyCommandContract contract)
    {
        var registry = CommandContractRegistry.CreateDefault();
        var runtimeType = GetApplicationRuntimeType(contract);
        var descriptor = new CommandDescriptor(registry, runtimeType);

        Assert.That(descriptor.CanonicalId, Is.EqualTo(contract.CanonicalId));
        Assert.That(descriptor.RuntimeType, Is.EqualTo(runtimeType));
    }

    [Test]
    public void Constructor_WithNullType_ThrowsArgumentNullException()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var action = () => new CommandDescriptor(registry, null!);

        Assert.That(Assert.Catch<ArgumentNullException>(() => { action(); })!.ParamName, Is.EqualTo("runtimeType"));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void FromPersistedId_StoresKnownCanonicalId(LegacyCommandContract contract)
    {
        var registry = CommandContractRegistry.CreateDefault();
        var descriptor = CommandDescriptor.FromPersistedId(registry, contract.CanonicalId);

        Assert.That(descriptor.CanonicalId, Is.EqualTo(contract.CanonicalId));
        Assert.That(descriptor.RuntimeType, Is.EqualTo(GetApplicationRuntimeType(contract)));
    }

    [Test]
    public void FromPersistedId_WithNullOrWhitespace_ThrowsArgumentException()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var nullAction = () => CommandDescriptor.FromPersistedId(registry, null!);
        var emptyAction = () => CommandDescriptor.FromPersistedId(registry, "");
        var whitespaceAction = () => CommandDescriptor.FromPersistedId(registry, "   ");

        Assert.That(() => { nullAction(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(() => { emptyAction(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(() => { whitespaceAction(); }, Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void IsExactTypeMatch_UsesKnownCanonicalIdsOnly()
    {
        var firstContract = LegacyCommandContractManifest.All[0];
        var secondContract = LegacyCommandContractManifest.All[1];
        var registry = CommandContractRegistry.CreateDefault();
        var firstDescriptor = new CommandDescriptor(registry, GetApplicationRuntimeType(firstContract));
        var sameDescriptor = CommandDescriptor.FromPersistedId(registry, firstContract.FutureClrNameReadAlias);
        var differentDescriptor = new CommandDescriptor(registry, GetApplicationRuntimeType(secondContract));

        Assert.That(firstDescriptor, Is.EqualTo(sameDescriptor));
        Assert.That(firstDescriptor, Is.Not.EqualTo(differentDescriptor));
        Assert.That(firstDescriptor.Equals(null), Is.False);
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void ResolveCommandType_ReadsKnownCanonicalId(LegacyCommandContract contract)
    {
        var registry = CommandContractRegistry.CreateDefault();

        Assert.That(CommandDescriptor.FromPersistedId(registry, contract.CanonicalId).RuntimeType, Is.EqualTo(GetApplicationRuntimeType(contract)));
    }

    [Test]
    public void ResolveCommandType_WithUnknownId_ThrowsInvalidOperationException()
    {
        const string unknownId = "NonExistent.Command";
        var registry = CommandContractRegistry.CreateDefault();
        var action = () => CommandDescriptor.FromPersistedId(registry, unknownId);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{unknownId}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void ResolveCommandType_WithNullOrWhitespace_ThrowsArgumentException()
    {
        var registry = CommandContractRegistry.CreateDefault();
        var nullAction = () => CommandDescriptor.FromPersistedId(registry, null!);
        var emptyAction = () => CommandDescriptor.FromPersistedId(registry, "");
        var whitespaceAction = () => CommandDescriptor.FromPersistedId(registry, "   ");

        Assert.That(() => { nullAction(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(() => { emptyAction(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(() => { whitespaceAction(); }, Throws.InstanceOf<ArgumentException>());
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void PersistenceRoundTrip_PreservesKnownCommandIdentity(LegacyCommandContract contract)
    {
        var registry = CommandContractRegistry.CreateDefault();
        var originalDescriptor = new CommandDescriptor(registry, GetApplicationRuntimeType(contract));
        var restoredDescriptor = CommandDescriptor.FromPersistedId(registry, originalDescriptor.CanonicalId);

        Assert.That(restoredDescriptor.RuntimeType, Is.EqualTo(GetApplicationRuntimeType(contract)));
        Assert.That(restoredDescriptor, Is.EqualTo(originalDescriptor));
        Assert.That(restoredDescriptor.GetHashCode(), Is.EqualTo(originalDescriptor.GetHashCode()));
        Assert.That(restoredDescriptor.ToString(), Is.EqualTo(contract.CanonicalId));
    }

    private static Type GetApplicationRuntimeType(LegacyCommandContract contract) => contract.CommandType;
}
