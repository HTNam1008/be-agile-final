using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;

public sealed record TopUpAccountSelection(
    TopUpAccountSelectionMode Mode,
    TopUpAccountFilter? Filter,
    IReadOnlyCollection<long> SelectedEducationAccountIds,
    IReadOnlyCollection<long> ExcludedEducationAccountIds)
{
    public static TopUpAccountSelection Explicit(IReadOnlyCollection<long> selectedEducationAccountIds)
        => new(
            TopUpAccountSelectionMode.ExplicitIds,
            null,
            selectedEducationAccountIds,
            Array.Empty<long>());

    public static TopUpAccountSelection AllMatching(
        TopUpAccountFilter filter,
        IReadOnlyCollection<long> excludedEducationAccountIds)
        => new(
            TopUpAccountSelectionMode.AllMatchingFilter,
            filter,
            Array.Empty<long>(),
            excludedEducationAccountIds);
}
