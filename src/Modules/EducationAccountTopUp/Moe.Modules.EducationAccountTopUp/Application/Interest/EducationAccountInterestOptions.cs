namespace Moe.Modules.EducationAccountTopUp.Application.Interest;

public sealed class EducationAccountInterestOptions
{
    public const string SectionName = "EducationAccountInterest";

    public bool Enabled { get; set; } = true;
    public decimal AnnualRate { get; set; } = 0.02m;
    public string RunAtUtc { get; set; } = "18:30";
    public int FirstInterestYear { get; set; } = 2026;
}

internal static class EducationAccountInterestCodes
{
    public const string TransactionTypeCode = "INTEREST_CREDIT";
    public const string ReferenceTypeCode = "ANNUAL_INTEREST";
    public const string Category = "INTEREST";

    public static string BuildIdempotencyKey(int interestYear, long educationAccountId)
        => $"interest:{interestYear}:{educationAccountId}";
}

internal sealed record AnnualInterestProcessingResult(
    int InterestYear,
    int ProcessedCount,
    int SkippedCount,
    decimal TotalInterestAmount);
