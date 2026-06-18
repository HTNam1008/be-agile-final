namespace Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

public sealed record TopUpStudentSearchSummaryPage(
    IReadOnlyList<TopUpStudentSearchSummary> Items,
    int Page,
    int PageSize,
    long TotalCount);
