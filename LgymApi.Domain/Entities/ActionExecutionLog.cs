using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

/// <summary>
/// Per-action execution log for tracking attempt outcomes on a CommandEnvelope.
/// </summary>
public sealed class ActionExecutionLog : EntityBase
{
    public ActionExecutionLog()
    {

    }

    public Guid CommandEnvelopeId { get; set; }

    public CommandEnvelope CommandEnvelope { get; set; } = null!;

    public ActionExecutionLogType ActionType { get; set; }

    public string? HandlerTypeName { get; set; }

    public ActionExecutionStatus Status { get; set; }

    public int AttemptNumber { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ErrorDetails { get; set; }
}
