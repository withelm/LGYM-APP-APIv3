using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.BackgroundWorker;

public sealed partial class BackgroundActionOrchestratorService
{
    private async Task MarkDeadLetteredAsync(
        CommandEnvelope envelope,
        ActionExecutionLog envelopeAttemptLog,
        string reason,
        Exception exception,
        CancellationToken cancellationToken)
    {
        envelopeAttemptLog.Status = ActionExecutionStatus.Failed;
        envelopeAttemptLog.ErrorMessage = exception.Message;
        envelopeAttemptLog.ErrorDetails = exception.ToString();
        envelope.MarkDeadLettered(reason, exception.ToString());
        await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordCancelledAttemptAsync(
        CommandEnvelope envelope,
        ActionExecutionLog envelopeAttemptLog)
    {
        const string cancellationMessage = "Command orchestration was cancelled.";
        envelopeAttemptLog.Status = ActionExecutionStatus.Failed;
        envelopeAttemptLog.ErrorMessage = cancellationMessage;
        envelopeAttemptLog.ErrorDetails = cancellationMessage;
        envelope.RecordAttemptFailure(cancellationMessage, cancellationMessage);
        await _commandEnvelopeRepository.UpdateAsync(envelope, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
