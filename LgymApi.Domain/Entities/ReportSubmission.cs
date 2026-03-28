using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportSubmission : EntityBase<ReportSubmission>
{
    public Id<ReportRequest> ReportRequestId { get; set; }
    public Id<User> TraineeId { get; set; }
    public string PayloadJson { get; set; } = "{}";

    public ReportRequest ReportRequest { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
