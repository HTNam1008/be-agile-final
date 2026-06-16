using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal sealed class SettlementPreference : Entity<long>
{
    private SettlementPreference() : base(0) { }

    public long EducationAccountId { get; private set; }
    public string DestinationTypeCode { get; private set; } = string.Empty;
    public string DestinationToken { get; private set; } = string.Empty;
    public string DestinationMasked { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}
