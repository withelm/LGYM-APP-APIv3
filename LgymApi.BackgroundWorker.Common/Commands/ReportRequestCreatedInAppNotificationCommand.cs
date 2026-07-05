using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainer creates a report request for a trainee.
/// Triggers in-app notification creation for the trainee.
/// </summary>
public sealed class ReportRequestCreatedInAppNotificationCommand : IActionCommand
{
    public Id<ReportRequest> RequestId { get; init; }
    public Id<User> TraineeId { get; init; }
    public Id<User> TrainerId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
}
