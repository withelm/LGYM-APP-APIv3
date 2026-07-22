using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public sealed class ReportSubmissionAcceptedProgressCommand : IActionCommand
{
    public required ReportSubmissionAcceptedProgressEvent Event { get; init; }

    public ReportSubmissionAcceptedProgressValidationResult Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        return Event.Validate();
    }
}
