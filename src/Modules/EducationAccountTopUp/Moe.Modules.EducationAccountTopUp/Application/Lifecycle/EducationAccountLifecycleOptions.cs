namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

public sealed class EducationAccountLifecycleOptions
{
    public const string SectionName = "EducationAccountLifecycle";

    public bool Enabled { get; set; } = true;
    public string RunAtUtc { get; set; } = "18:00";
    public int PollIntervalSeconds { get; set; } = 60;
}
