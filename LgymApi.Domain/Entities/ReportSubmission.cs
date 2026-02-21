namespace LgymApi.Domain.Entities;

public sealed class ReportSubmission : EntityBase
{
    public Guid ReportRequestId { get; set; }
    public Guid TraineeId { get; set; }
    public string PayloadJson { get; set; } = "{}";

    public ReportRequest ReportRequest { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
