namespace Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

public sealed record PreviewCampaignRequest(
    int PageNumber = 1,
    int PageSize = 50
);
