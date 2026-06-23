namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class TopUpTransactionResultsRequest
{
    public string? Status { get; init; }
    public string? StudentOrAccountSearch { get; init; }
    public string? Reason { get; init; }
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
