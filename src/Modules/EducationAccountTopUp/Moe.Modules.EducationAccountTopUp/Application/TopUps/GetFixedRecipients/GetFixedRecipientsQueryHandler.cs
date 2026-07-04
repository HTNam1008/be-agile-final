using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetFixedRecipients;

internal sealed class GetFixedRecipientsQueryHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpCampaignReader reader,
    ICurrentUser currentUser,
    IClock clock,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students)
    : IQueryHandler<GetFixedRecipientsQuery, IReadOnlyList<FixedRecipientDto>>
{
    public async Task<Result<IReadOnlyList<FixedRecipientDto>>> Handle(
        GetFixedRecipientsQuery query,
        CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(query.TopUpCampaignId, cancellationToken);

        if (campaign is null)
        {
            return Result<IReadOnlyList<FixedRecipientDto>>.Failure(TopUpErrors.CampaignNotFound);
        }

        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId)
            && currentUser.OrganizationUnitId != campaign.OrganizationId)
        {
            return Result<IReadOnlyList<FixedRecipientDto>>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        var recipients = await reader.GetActiveRecipientsAsync(query.TopUpCampaignId, cancellationToken);

        if (recipients.Count == 0)
        {
            return Result<IReadOnlyList<FixedRecipientDto>>.Success(Array.Empty<FixedRecipientDto>());
        }

        long[] educationAccountIds = recipients.Select(x => x.EducationAccountId).ToArray();

        IReadOnlyDictionary<long, TopUpAccountProjection> accountById =
            await accounts.FindByEducationAccountIdsAsync(educationAccountIds, cancellationToken);

        long[] personIds = accountById.Values.Select(x => x.PersonId).Distinct().ToArray();

        var studentCriteria = new TopUpStudentSearchCriteria(
            null,
            personIds,
            null,
            campaign.OrganizationId,
            null,
            null,
            null,
            null,
            null,
            1,
            personIds.Length > 0 ? personIds.Length : 1);

        TopUpStudentSearchSummaryPage studentPage = await students.SearchForTopUpAsync(
            studentCriteria,
            new[] { campaign.OrganizationId },
            cancellationToken);

        IReadOnlyDictionary<long, TopUpStudentSearchSummary> studentByPersonId = studentPage.Items.ToDictionary(x => x.PersonId);

        var dtos = new List<FixedRecipientDto>(recipients.Count);
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        foreach (var recipient in recipients)
        {
            if (!accountById.TryGetValue(recipient.EducationAccountId, out var account))
            {
                continue;
            }

            if (!studentByPersonId.TryGetValue(account.PersonId, out var student))
            {
                continue;
            }

            int age = today.Year - student.DateOfBirth.Year;
            if (student.DateOfBirth > today.AddYears(-age))
            {
                age--;
            }

            dtos.Add(new FixedRecipientDto(
                recipient.EducationAccountId,
                student.DisplayName,
                account.AccountNumber,
                TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber),
                student.StudentNumber,
                age,
                account.Balance,
                student.SchoolingStatusCode,
                recipient.AmountOverride
            ));
        }

        return Result<IReadOnlyList<FixedRecipientDto>>.Success(dtos);
    }
}
