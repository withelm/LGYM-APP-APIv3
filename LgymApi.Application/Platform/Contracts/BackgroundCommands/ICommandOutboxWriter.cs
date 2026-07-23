using LgymApi.Domain.Entities;

namespace LgymApi.Application.Platform.Contracts.BackgroundCommands;

public interface ICommandOutboxWriter
{
    Task<CommandEnvelopeStageResult> StageAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : class, IActionCommand;
}

public sealed record CommandEnvelopeStageResult(CommandEnvelope? Envelope, bool WasExisting);
