using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandOutboxCompatibilityTests
{
    [Test]
    public async Task AcceptedProgressWriter_PersistsCanonicalLegacyIdAndGoldenPayload()
    {
        var contract = LegacyCommandContractManifest.All.Single(row =>
            row.CommandType == typeof(ReportSubmissionAcceptedProgressCommand));
        var command = (ReportSubmissionAcceptedProgressCommand)contract.Command;
        var resolver = Substitute.For<IBackgroundActionResolver>();
        resolver.GetHandlerTypeNames(typeof(ReportSubmissionAcceptedProgressCommand))
            .Returns(contract.HandlerTypeFullNames);
        var runtime = Substitute.For<ICommandEnvelopeRuntime>();
        CommandEnvelopeRequest? capturedRequest = null;
        runtime.StageAsync(Arg.Do<CommandEnvelopeRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandEnvelopeReceipt("envelope-id", false)));
        var writer = new CommandOutboxWriter(
            resolver,
            CommandContractRegistry.CreateDefault(),
            runtime,
            NullLogger<CommandOutboxWriter>.Instance);

        var result = await writer.StageAsync(command);

        Assert.That(capturedRequest, Is.Not.Null);
        var assertedChain38 = capturedRequest!.CommandId;
        Assert.That(assertedChain38, Is.EqualTo(contract.CanonicalId));
        Assert.That(assertedChain38, Is.Not.EqualTo(contract.FutureClrNameReadAlias));
        Assert.That(capturedRequest.PayloadJson, Is.EqualTo(contract.PayloadJson));
        Assert.That($"{capturedRequest.CommandId}|{capturedRequest.PayloadJson}", Is.EqualTo(contract.CorrelationInput));
        Assert.That(result.EnvelopeId, Is.EqualTo("envelope-id"));
        Assert.That(result.WasExisting, Is.False);
    }
}
