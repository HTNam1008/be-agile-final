using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal sealed class AccountSettlement : Entity<long>
{
    private AccountSettlement() : base(0) { }

    public long EducationAccountId { get; private set; }
    public long? SettlementPreferenceId { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public decimal SettlementAmount { get; private set; }
    public string DestinationTypeCode { get; private set; } = string.Empty;
    public string DestinationToken { get; private set; } = string.Empty;
    public string DestinationMasked { get; private set; } = string.Empty;
    public string SettlementStatusCode { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
}
