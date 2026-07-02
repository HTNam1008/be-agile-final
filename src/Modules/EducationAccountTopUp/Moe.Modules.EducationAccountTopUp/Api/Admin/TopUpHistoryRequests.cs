namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class CampaignHistoryRequest
{
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public long? CampaignId { get; init; }
    public string? CampaignSearch { get; init; }
    public long? OrganizationId { get; init; }
    public string? Status { get; init; }
    public long? ActorId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class RunHistoryRequest
{
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public long? CampaignId { get; init; }
    public string? CampaignSearch { get; init; }
    public long? OrganizationId { get; init; }
    public string? TriggerType { get; init; }
    public string? Status { get; init; }
    public string? StudentOrAccountSearch { get; init; }
    public long? ActorId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class CampaignTransactionHistoryRequest
{
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public long? OrganizationId { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class AccountTransactionHistoryRequest
{
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class AllTransactionsRequest
{
    public DateTime? DateFromUtc { get; init; }
    public DateTime? DateToUtc { get; init; }
    public string? CampaignSearch { get; init; }
    public long? OrganizationId { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
