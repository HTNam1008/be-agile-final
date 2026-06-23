using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class CoursePaymentPlan : Entity<long>
{
    private CoursePaymentPlan() : base(0) { }

    private CoursePaymentPlan(
        long courseId,
        string displayName,
        string planTypeCode,
        int installmentCount,
        int version,
        DateTime createdAtUtc) : base(0)
    {
        CourseId = courseId;
        DisplayName = displayName.Trim();
        PlanTypeCode = planTypeCode;
        InstallmentCount = installmentCount;
        IntervalMonths = 1;
        Version = version;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public long CourseId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string PlanTypeCode { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "SGD";
    public int InstallmentCount { get; private set; }
    public int IntervalMonths { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<CoursePaymentPlan> Create(
        long courseId,
        string displayName,
        string planTypeCode,
        int installmentCount,
        int version,
        DateTime createdAtUtc)
    {
        if (courseId <= 0 || string.IsNullOrWhiteSpace(displayName) || version <= 0)
            return Result<CoursePaymentPlan>.Failure(PaymentDomainErrors.InvalidPaymentPlan);

        bool fullPayment = planTypeCode == PaymentPlanTypeCodes.FullPayment;
        bool installment = planTypeCode == PaymentPlanTypeCodes.Installment;
        if ((!fullPayment && !installment) ||
            (fullPayment && installmentCount != 1) ||
            (installment && installmentCount is not (3 or 6)))
        {
            return Result<CoursePaymentPlan>.Failure(PaymentDomainErrors.InvalidPaymentPlan);
        }

        return Result<CoursePaymentPlan>.Success(new(
            courseId,
            displayName,
            planTypeCode,
            installmentCount,
            version,
            createdAtUtc));
    }

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
    }
}

public static class PaymentPlanTypeCodes
{
    public const string FullPayment = "FULL_PAYMENT";
    public const string Installment = "INSTALLMENT";
}
