namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed record RecipientInfo
{
    public required long EducationAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required long OrganizationUnitId { get; init; }
    public required string CampaignReason { get; init; }
}
