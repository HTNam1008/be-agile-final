namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class TopUpCampaignStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Paused = "PAUSED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] ValidStatuses = { Draft, Active, Paused, Completed, Cancelled };

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return ValidStatuses.Contains(status.ToUpperInvariant());
    }
}
