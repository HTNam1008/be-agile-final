using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class DynamicTopUpContract : Entity<long>
{
    private DynamicTopUpContract() : base(0) { }

    public long TopUpCampaignId { get; private set; }
    public long EducationAccountId { get; private set; }
    public string DeliveryTypeCode { get; private set; } = string.Empty;
    public DateTime QualifiedAtUtc { get; private set; }

    public decimal AmountPerPayment { get; private set; }
    public decimal MaxTotalAmount { get; private set; }
    public string FrequencyCode { get; private set; } = string.Empty;
    public int FrequencyInterval { get; private set; }

    public decimal TotalReceived { get; private set; }
    public int CyclesCompleted { get; private set; }
    public DateTime? FirstPaymentDate { get; private set; }
    public DateTime? NextPaymentDate { get; private set; }
    public string ContractStatus { get; private set; } = ContractStatuses.Active;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public int? DerivedTotalCycles =>
        DeliveryTypeCode == DeliveryType.FixedContract
            ? (int)(MaxTotalAmount / AmountPerPayment) : null;

    public bool IsCompleted => ContractStatus != ContractStatuses.Active;

    public bool CanPayAt(DateTime nowUtc) =>
        ContractStatus == ContractStatuses.Active
        && NextPaymentDate.HasValue
        && NextPaymentDate.Value <= nowUtc
        && TotalReceived < MaxTotalAmount;

    public static DynamicTopUpContract Create(
        long campaignId, long accountId,
        string deliveryTypeCode,
        decimal amountPerPayment, decimal maxTotalAmount,
        string frequencyCode, int frequencyInterval,
        DateTime qualifiedAtUtc, DateTime nextPaymentDate)
    {
        return new DynamicTopUpContract
        {
            TopUpCampaignId = campaignId,
            EducationAccountId = accountId,
            DeliveryTypeCode = deliveryTypeCode,
            QualifiedAtUtc = qualifiedAtUtc,
            AmountPerPayment = amountPerPayment,
            MaxTotalAmount = maxTotalAmount,
            TotalReceived = 0, CyclesCompleted = 0,
            FrequencyCode = frequencyCode,
            FrequencyInterval = frequencyInterval,
            FirstPaymentDate = nextPaymentDate,
            NextPaymentDate = nextPaymentDate,
            ContractStatus = ContractStatuses.Active,
            CreatedAtUtc = qualifiedAtUtc,
        };
    }

    public Result RecordPayment(decimal amount, DateTime paidAtUtc)
    {
        if (IsCompleted) return Result.Failure(TopUpErrors.ContractAlreadyCompleted);
        if (TotalReceived + amount > MaxTotalAmount)
            amount = MaxTotalAmount - TotalReceived;

        TotalReceived += amount;
        CyclesCompleted++;
        UpdatedAtUtc = paidAtUtc;

        if (TotalReceived >= MaxTotalAmount)
        {
            ContractStatus = ContractStatuses.Completed;
            NextPaymentDate = null;
            return Result.Success();
        }

        if (DeliveryTypeCode == DeliveryType.FixedContract
            && DerivedTotalCycles.HasValue
            && CyclesCompleted >= DerivedTotalCycles.Value)
        {
            ContractStatus = ContractStatuses.Completed;
            NextPaymentDate = null;
            return Result.Success();
        }

        return Result.Success();
    }

    public void SetNextPaymentDate(DateTime? nextDate, DateTime nowUtc)
    { NextPaymentDate = nextDate; UpdatedAtUtc = nowUtc; }

    public void Complete(DateTime nowUtc)
    { ContractStatus = ContractStatuses.Completed; NextPaymentDate = null; UpdatedAtUtc = nowUtc; }

    public void Suspend(DateTime nowUtc)
    { ContractStatus = ContractStatuses.Suspended; NextPaymentDate = null; UpdatedAtUtc = nowUtc; }
}

public static class ContractStatuses
{
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string Suspended = "SUSPENDED";
}
