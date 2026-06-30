namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class TopUpWorkerOptions
{
    public const string SectionName = "TopUpWorker";
    public TimeSpan AssessmentLockTtl { get; set; } = TimeSpan.FromMinutes(15);
    public int AssessmentPollIntervalSeconds { get; set; } = 60;
}
