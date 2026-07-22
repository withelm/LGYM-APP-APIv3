using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Runtime;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker;

public sealed class CommandOutboxWriter : ICommandOutboxWriter
{
    private readonly IBackgroundActionResolver _backgroundActionResolver;
    private readonly CommandContractRegistry _commandContractRegistry;
    private readonly ICommandEnvelopeRepository _commandEnvelopeRepository;
    private readonly ILogger<CommandOutboxWriter> _logger;

    public CommandOutboxWriter(
        IBackgroundActionResolver backgroundActionResolver,
        CommandContractRegistry commandContractRegistry,
        ICommandEnvelopeRepository commandEnvelopeRepository,
        ILogger<CommandOutboxWriter> logger)
    {
        _backgroundActionResolver = backgroundActionResolver;
        _commandContractRegistry = commandContractRegistry;
        _commandEnvelopeRepository = commandEnvelopeRepository;
        _logger = logger;
    }

    public async Task<CommandEnvelopeStageResult> StageAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : class, IActionCommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = typeof(TCommand);
        var handlerCount = _backgroundActionResolver.GetHandlerTypeNames(commandType).Count;
        if (handlerCount == 0)
        {
            _logger.LogWarning(
                "No handlers registered for command type {CommandType}. Skipping durable envelope staging.",
                commandType.FullName);
            return new CommandEnvelopeStageResult(null, false);
        }

        var envelope = CommandEnvelopeFactory.Create(command, _commandContractRegistry);
        var envelopeResult = await _commandEnvelopeRepository.AddOrGetExistingAsync(envelope, cancellationToken);
        var wasExisting = !ReferenceEquals(envelopeResult, envelope);

        if (wasExisting)
        {
            _logger.LogInformation(
                "Command envelope already exists for correlation {CorrelationId} (envelope {EnvelopeId}).",
                envelope.CorrelationId,
                envelopeResult.Id);
        }
        else
        {
            _logger.LogInformation(
                "Staged command {CommandId} with correlation {CorrelationId}.",
                envelope.CommandTypeFullName,
                envelope.CorrelationId);
        }

        return new CommandEnvelopeStageResult(envelopeResult, wasExisting);
    }
}
