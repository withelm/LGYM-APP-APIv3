using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.Identity.Contracts.AccountDeletion;
using NUnit.Framework;
using System.Text.Json;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandEnvelopeCompatibilityTests
{
    [Test]
    public void AccountDeletionScheduled_RegistryWritesCanonicalIdAndReadsBothIdsWithGoldenPayload()
    {
        const string canonicalId = "LgymApi.BackgroundWorker.Common.Commands.AccountDeletionScheduledCommand";
        const string payload = "{\"accountId\":\"00000000-0000-0000-0000-000000000001\",\"deletionGeneration\":\"00000000-0000-0000-0000-000000000002\"}";
        var registry = CommandContractRegistry.CreateDefault();
        var type = typeof(AccountDeletionScheduledCommand);
        var descriptor = registry.DescribeForWrite(type);
        var contract = registry.Contracts.Single(row => row.RuntimeType == type);

        Assert.That(descriptor.CanonicalId, Is.EqualTo(canonicalId));
        Assert.That(contract.ReadAlias, Is.EqualTo(type.FullName));
        Assert.That(contract.ExpectedHandlerTypes, Is.EqualTo(new[] { typeof(AccountDeletionScheduledCommandHandler) }));
        foreach (var id in new[] { canonicalId, type.FullName! })
        {
            var resolved = registry.Resolve(id);
            var command = JsonSerializer.Deserialize(payload, resolved.RuntimeType, SharedSerializationOptions.Current);
            Assert.That(resolved.RuntimeType, Is.EqualTo(type));
            Assert.That(command, Is.TypeOf<AccountDeletionScheduledCommand>());
            Assert.That(JsonSerializer.Serialize(command, resolved.RuntimeType, SharedSerializationOptions.Current), Is.EqualTo(payload));
        }
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void OldFixture_ToNewReader_ResolvesAllFifteenCanonicalIds(LegacyCommandContract contract)
    {
        var resolvedDescriptor = CommandContractRegistry.CreateDefault().Resolve(contract.CanonicalId);
        var command = JsonSerializer.Deserialize(
            contract.PayloadJson,
            resolvedDescriptor.RuntimeType,
            SharedSerializationOptions.Current);

        Assert.That(resolvedDescriptor.CanonicalId, Is.EqualTo(contract.CanonicalId));
        var assertedChain36 = command;
        Assert.That(assertedChain36, Is.Not.Null);
        Assert.That(assertedChain36, Is.TypeOf(resolvedDescriptor.RuntimeType));
        Assert.That(JsonSerializer.Serialize(command, resolvedDescriptor.RuntimeType, SharedSerializationOptions.Current), Is.EqualTo(contract.PayloadJson));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void NewWriter_ToOldCompatibleFixture_WritesCanonicalIdAndGoldenPayload(LegacyCommandContract contract)
    {
        var registry = CommandContractRegistry.CreateDefault();
        var runtimeType = contract.CommandType;
        var command = JsonSerializer.Deserialize(
            contract.PayloadJson,
            runtimeType,
            SharedSerializationOptions.Current)!;
        var persistedId = registry.DescribeForWrite(runtimeType).CanonicalId;
        var payloadJson = JsonSerializer.Serialize(
            command,
            runtimeType,
            SharedSerializationOptions.Current);

        Assert.That(persistedId, Is.EqualTo(contract.CanonicalId));
        Assert.That(payloadJson, Is.EqualTo(contract.PayloadJson));
        Assert.That($"{persistedId}|{payloadJson}", Is.EqualTo(contract.CorrelationInput));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void NewAlias_ToNewReader_ResolvesToTheManifestRuntimeType(LegacyCommandContract contract)
    {
        var resolvedDescriptor = CommandContractRegistry.CreateDefault().Resolve(contract.FutureClrNameReadAlias);

        Assert.That(resolvedDescriptor.CanonicalId, Is.EqualTo(contract.CanonicalId));
        Assert.That(resolvedDescriptor.RuntimeType.FullName, Is.EqualTo(contract.FutureClrNameReadAlias));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void NewAlias_ToOldReader_IsUnsupported_AndNoWriterEmitsAlias(LegacyCommandContract contract)
    {
        Assert.That(LegacyCommandContractManifest.ResolveForLegacyReader(contract.CanonicalId), Is.EqualTo(contract.CommandType));

        var oldReader = () => LegacyCommandContractManifest.ResolveForLegacyReader(contract.FutureClrNameReadAlias);

        Assert.That(() => { oldReader(); }, Throws.InstanceOf<InvalidOperationException>());
        var registry = CommandContractRegistry.CreateDefault();
        var runtimeType = contract.CommandType;

        Assert.That(registry.Contracts.Select(row => row.CanonicalId), Does.Not.Contain(contract.FutureClrNameReadAlias));
        var assertedChain37 = registry.DescribeForWrite(runtimeType).CanonicalId;
        Assert.That(assertedChain37, Is.EqualTo(contract.CanonicalId));
        Assert.That(assertedChain37, Is.Not.EqualTo(contract.FutureClrNameReadAlias));
    }

    [Test]
    public void UnknownPersistedId_ToNewReader_IsRejected()
    {
        var action = () => CommandContractRegistry.CreateDefault().Resolve("Unknown.Command");

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Unknown durable command identifier 'Unknown.Command'.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }
}
