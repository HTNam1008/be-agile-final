using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;

public sealed class TopUpAccountSelectionValidator : AbstractValidator<TopUpAccountSelection>
{
    public const int MaxExplicitSelectionIds = 1000;
    public const int MaxExcludedSelectionIds = 1000;

    public TopUpAccountSelectionValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.SelectedEducationAccountIds).NotNull();
        RuleFor(x => x.ExcludedEducationAccountIds).NotNull();

        When(x => x.Mode == TopUpAccountSelectionMode.ExplicitIds, () =>
        {
            RuleFor(x => x.Filter).Null();
            RuleFor(x => x.SelectedEducationAccountIds)
                .NotEmpty()
                .Must(HaveBoundedUniquePositiveIds)
                .WithMessage($"SelectedEducationAccountIds must contain unique positive ids and at most {MaxExplicitSelectionIds} ids.");
            RuleFor(x => x.ExcludedEducationAccountIds).Empty();
        });

        When(x => x.Mode == TopUpAccountSelectionMode.AllMatchingFilter, () =>
        {
            RuleFor(x => x.Filter).NotNull().SetValidator(new TopUpAccountFilterValidator()!);
            RuleFor(x => x.SelectedEducationAccountIds).Empty();
            RuleFor(x => x.ExcludedEducationAccountIds)
                .Must(HaveBoundedUniquePositiveExclusionIds)
                .WithMessage($"ExcludedEducationAccountIds must contain unique positive ids and at most {MaxExcludedSelectionIds} ids.");
        });
    }

    private static bool HaveBoundedUniquePositiveIds(IReadOnlyCollection<long> ids)
        => HaveBoundedUniquePositiveIds(ids, MaxExplicitSelectionIds);

    private static bool HaveBoundedUniquePositiveExclusionIds(IReadOnlyCollection<long> ids)
        => HaveBoundedUniquePositiveIds(ids, MaxExcludedSelectionIds);

    private static bool HaveBoundedUniquePositiveIds(IReadOnlyCollection<long> ids, int maxCount)
        => ids.Count <= maxCount && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count;
}
