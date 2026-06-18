using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed record UpsertFixedRecipientsRequest(
    TopUpAccountSelectionMode Mode,
    TopUpAccountFilter? Filter,
    IReadOnlyCollection<UpsertFixedRecipientDto> Recipients,
    IReadOnlyCollection<long> ExcludedEducationAccountIds);
