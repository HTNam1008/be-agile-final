namespace Moe.Modules.EducationAccountTopUp.Application.Interest;

internal interface IAnnualInterestProcessor
{
    Task<AnnualInterestProcessingResult> ProcessDueInterestAsync(
        DateOnly todayInSingapore,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);
}
