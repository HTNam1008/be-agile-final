namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;

public sealed record TopUpAccountSelectionResolution(
    IReadOnlyCollection<long> EducationAccountIds,
    int TotalMatched,
    int TotalExcluded,
    int TotalSelected);
