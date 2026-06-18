using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps;

internal static class TopUpErrors
{
    public static readonly Error AdminOrganizationScopeRequired = new(
        "TOPUP.ADMIN_ORGANIZATION_SCOPE_REQUIRED",
        "An admin organization scope is required.");

    public static readonly Error OrganizationOutsideScope = new(
        "TOPUP.ORGANIZATION_OUTSIDE_SCOPE",
        "The requested organization is outside the admin's scope.");

    public static readonly Error InvalidAccountSelection = new(
        "TOPUP.INVALID_ACCOUNT_SELECTION",
        "The top-up account selection is invalid.");

    public static readonly Error AccountSelectionOutsideScope = new(
        "TOPUP.ACCOUNT_SELECTION_OUTSIDE_SCOPE",
        "The top-up account selection contains accounts outside the admin's scope or filter.");

    public static readonly Error CampaignNotFound = new(
        "TOPUP.CAMPAIGN_NOT_FOUND",
        "The top-up campaign was not found.");

    public static readonly Error InvalidRecipientMode = new(
        "TOPUP.INVALID_RECIPIENT_MODE",
        "Recipients can only be selected directly for a fixed-selection campaign.");

    public static readonly Error InvalidCampaignStatus = new(
        "TOPUP.INVALID_CAMPAIGN_STATUS",
        "Recipients can only be modified for a draft or paused campaign.");
}
