using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class UpsertFixedRecipientsRequestValidator : AbstractValidator<UpsertFixedRecipientsRequest>
{
    public UpsertFixedRecipientsRequestValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Recipients).NotNull();
        RuleFor(x => x.ExcludedEducationAccountIds).NotNull();

        RuleForEach(x => x.Recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(x => x.EducationAccountId).GreaterThan(0);
            recipient.When(x => x.AmountOverride.HasValue, () =>
            {
                recipient.RuleFor(x => x.AmountOverride!.Value).GreaterThan(0);
            });
        });

        When(x => x.Mode == TopUpAccountSelectionMode.ExplicitIds, () =>
        {
            RuleFor(x => x.Filter).Null();
            RuleFor(x => x.Recipients)
                .NotEmpty()
                .Must(HaveBoundedUniqueRecipientIds)
                .WithMessage(
                    $"Recipients must contain unique positive account ids and at most {TopUpAccountSelectionValidator.MaxExplicitSelectionIds} items.");
            RuleFor(x => x.ExcludedEducationAccountIds).Empty();
        });

        When(x => x.Mode == TopUpAccountSelectionMode.AllMatchingFilter, () =>
        {
            RuleFor(x => x.Filter).NotNull();
            RuleFor(x => x.Recipients).Empty();
            RuleFor(x => x.ExcludedEducationAccountIds)
                .Must(HaveBoundedUniquePositiveExclusionIds)
                .WithMessage(
                    $"ExcludedEducationAccountIds must contain unique positive ids and at most {TopUpAccountSelectionValidator.MaxExcludedSelectionIds} items.");
        });
    }

    private static bool HaveBoundedUniqueRecipientIds(
        IReadOnlyCollection<Application.TopUps.UpsertFixedRecipients.UpsertFixedRecipientDto> recipients)
        => recipients is not null
           && recipients.Count <= TopUpAccountSelectionValidator.MaxExplicitSelectionIds
           && recipients.All(x => x.EducationAccountId > 0)
           && recipients.Select(x => x.EducationAccountId).Distinct().Count() == recipients.Count;

    private static bool HaveBoundedUniquePositiveExclusionIds(IReadOnlyCollection<long> ids)
        => ids is not null
           && ids.Count <= TopUpAccountSelectionValidator.MaxExcludedSelectionIds
           && ids.All(id => id > 0)
           && ids.Distinct().Count() == ids.Count;
}
