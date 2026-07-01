using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal sealed class SettlementPreference : Entity<long>
{
    private SettlementPreference() : base(0) { }

    private SettlementPreference(
        long educationAccountId,
        string destinationTypeCode,
        string destinationToken,
        string destinationMasked,
        bool isVerified,
        DateTime updatedAtUtc) : base(0)
    {
        EducationAccountId = educationAccountId;
        DestinationTypeCode = destinationTypeCode;
        DestinationToken = destinationToken;
        DestinationMasked = destinationMasked;
        IsVerified = isVerified;
        IsActive = true;
        UpdatedAtUtc = updatedAtUtc;
    }

    public long EducationAccountId { get; private set; }
    public string DestinationTypeCode { get; private set; } = string.Empty;
    public string DestinationToken { get; private set; } = string.Empty;
    public string DestinationMasked { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static SettlementPreference Create(
        long educationAccountId,
        string destinationTypeCode,
        string destinationToken,
        string destinationMasked,
        bool isVerified,
        DateTime updatedAtUtc)
        => new(
            educationAccountId,
            destinationTypeCode,
            destinationToken,
            destinationMasked,
            isVerified,
            updatedAtUtc);

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
    }
}
