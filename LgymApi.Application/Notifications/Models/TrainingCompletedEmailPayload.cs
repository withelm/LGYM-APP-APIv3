using System.Globalization;
using System.Text.Json.Serialization;
using LgymApi.Application.Notifications;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Notifications.Models;

public sealed class TrainingCompletedEmailPayload : IEmailPayload
{
    public Guid UserId { get; init; }
    public Guid TrainingId { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";
    public string PlanDayName { get; init; } = string.Empty;
    public DateTimeOffset TrainingDate { get; init; }
    public IReadOnlyList<TrainingExerciseSummary> Exercises { get; init; } = Array.Empty<TrainingExerciseSummary>();

    public Guid CorrelationId => TrainingId;
    public string NotificationType => EmailNotificationTypes.TrainingCompleted;

    [JsonIgnore]
    public CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }
}

public sealed class TrainingExerciseSummary
{
    public string ExerciseName { get; init; } = string.Empty;
    public int Series { get; init; }
    public int Reps { get; init; }
    public double Weight { get; init; }
    public WeightUnits Unit { get; init; }
}
