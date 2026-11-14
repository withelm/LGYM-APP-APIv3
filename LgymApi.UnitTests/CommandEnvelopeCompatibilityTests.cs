using FluentAssertions;
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

        descriptor.CanonicalId.Should().Be(canonicalId);
        contract.ReadAlias.Should().Be(type.FullName);
        contract.ExpectedHandlerTypes.Should().Equal(typeof(AccountDeletionScheduledCommandHandler));
        foreach (var id in new[] { canonicalId, type.FullName! })
        {
            var resolved = registry.Resolve(id);
            var command = JsonSerializer.Deserialize(payload, resolved.RuntimeType, SharedSerializationOptions.Current);
            resolved.RuntimeType.Should().Be(type);
            command.Should().BeOfType<AccountDeletionScheduledCommand>();
            JsonSerializer.Serialize(command, resolved.RuntimeType, SharedSerializationOptions.Current).Should().Be(payload);
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

        resolvedDescriptor.CanonicalId.Should().Be(contract.CanonicalId);
        command.Should().NotBeNull().And.BeOfType(resolvedDescriptor.RuntimeType);
        JsonSerializer.Serialize(command, resolvedDescriptor.RuntimeType, SharedSerializationOptions.Current)
            .Should().Be(contract.PayloadJson);
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

        persistedId.Should().Be(contract.CanonicalId);
        payloadJson.Should().Be(contract.PayloadJson);
        $"{persistedId}|{payloadJson}".Should().Be(contract.CorrelationInput);
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void NewAlias_ToNewReader_ResolvesToTheManifestRuntimeType(LegacyCommandContract contract)
    {
        var resolvedDescriptor = CommandContractRegistry.CreateDefault().Resolve(contract.FutureClrNameReadAlias);

        resolvedDescriptor.CanonicalId.Should().Be(contract.CanonicalId);
        resolvedDescriptor.RuntimeType.FullName.Should().Be(contract.FutureClrNameReadAlias);
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void NewAlias_ToOldReader_IsUnsupported_AndNoWriterEmitsAlias(LegacyCommandContract contract)
    {
        LegacyCommandContractManifest.ResolveForLegacyReader(contract.CanonicalId)
            .Should().Be(contract.CommandType);

        var oldReader = () => LegacyCommandContractManifest.ResolveForLegacyReader(contract.FutureClrNameReadAlias);

        oldReader.Should().Throw<InvalidOperationException>();
        var registry = CommandContractRegistry.CreateDefault();
        var runtimeType = contract.CommandType;

        registry.Contracts.Select(row => row.CanonicalId)
            .Should().NotContain(contract.FutureClrNameReadAlias);
        registry.DescribeForWrite(runtimeType).CanonicalId
            .Should().Be(contract.CanonicalId)
            .And.NotBe(contract.FutureClrNameReadAlias);
    }

    [Test]
    public void UnknownPersistedId_ToNewReader_IsRejected()
    {
        var action = () => CommandContractRegistry.CreateDefault().Resolve("Unknown.Command");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown durable command identifier 'Unknown.Command'.");
    }
}
