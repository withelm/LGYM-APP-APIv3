using LgymApi.BackgroundWorker.Runtime;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandTypeDiscriminatorPolicyTests
{
    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void GetDiscriminator_WritesFixedCanonicalId_ForKnownCommand(LegacyCommandContract contract)
    {
        var policy = new CommandTypeDiscriminatorPolicy(CommandContractRegistry.CreateDefault());

        Assert.That(policy.GetDiscriminator(GetApplicationRuntimeType(contract)), Is.EqualTo(contract.CanonicalId));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void ResolveType_ReadsFixedCanonicalId_ForKnownCommand(LegacyCommandContract contract)
    {
        var policy = new CommandTypeDiscriminatorPolicy(CommandContractRegistry.CreateDefault());

        Assert.That(policy.ResolveType(contract.CanonicalId), Is.EqualTo(GetApplicationRuntimeType(contract)));
    }

    [Test]
    public void ResolveType_Throws_ForUnknownDurableCommandId()
    {
        var policy = new CommandTypeDiscriminatorPolicy(CommandContractRegistry.CreateDefault());
        var action = () => policy.ResolveType("Fake.Namespace.UnknownCommand");

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void IsExactMatch_CanonicalizesReadAliasBeforeComparison(LegacyCommandContract contract)
    {
        var policy = new CommandTypeDiscriminatorPolicy(CommandContractRegistry.CreateDefault());

        Assert.That(policy.IsExactMatch(contract.CanonicalId, contract.FutureClrNameReadAlias), Is.True);
    }

    [Test]
    public void IsExactMatch_ReturnsFalse_ForNullOrWhitespace()
    {
        var policy = new CommandTypeDiscriminatorPolicy(CommandContractRegistry.CreateDefault());

        Assert.That(policy.IsExactMatch("Known.Command", ""), Is.False);
        Assert.That(policy.IsExactMatch("", "Known.Command"), Is.False);
    }

    private static Type GetApplicationRuntimeType(LegacyCommandContract contract) => contract.CommandType;
}
