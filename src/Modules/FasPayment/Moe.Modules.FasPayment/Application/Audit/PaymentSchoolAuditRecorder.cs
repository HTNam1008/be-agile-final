using Moe.Application.Abstractions.Audit;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Application.Audit;

internal sealed class PaymentSchoolAuditRecorder(
    IAuditService audit,
    ICoursePaymentGateway coursePayments) : IPaymentSchoolAuditRecorder
{
    public async Task RecordPaymentAsync(
        string actionCode,
        Payment payment,
        IReadOnlyCollection<long> billIds,
        string summary,
        string? beforeStatus,
        string afterStatus,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<BillSchoolOrganization> billSchools =
            await coursePayments.FindBillOrganizationIdsAsync(ResolveBillIds(payment, billIds), cancellationToken);

        foreach (IGrouping<long, BillSchoolOrganization> schoolBills in billSchools.GroupBy(x => x.OrganizationId))
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    actionCode,
                    "Payment",
                    payment.Id,
                    schoolBills.Key,
                    new SchoolAuditDetails(
                        summary,
                        RelatedIds: BuildPaymentRelatedIds(payment, schoolBills.Select(x => x.BillId)),
                        StatusTransition: new SchoolAuditStatusTransition(beforeStatus, afterStatus),
                        ReasonCode: reasonCode,
                        Count: schoolBills.Count())),
                cancellationToken);
        }
    }

    public async Task RecordRefundAsync(
        string actionCode,
        Payment payment,
        long refundId,
        IReadOnlyCollection<long> billIds,
        string summary,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<BillSchoolOrganization> billSchools =
            await coursePayments.FindBillOrganizationIdsAsync(ResolveBillIds(payment, billIds), cancellationToken);

        foreach (IGrouping<long, BillSchoolOrganization> schoolBills in billSchools.GroupBy(x => x.OrganizationId))
        {
            Dictionary<string, long> relatedIds = BuildPaymentRelatedIds(payment, schoolBills.Select(x => x.BillId));
            relatedIds["refundId"] = refundId;

            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    actionCode,
                    "PaymentRefund",
                    refundId,
                    schoolBills.Key,
                    new SchoolAuditDetails(
                        summary,
                        RelatedIds: relatedIds,
                        ReasonCode: reasonCode,
                        Count: schoolBills.Count())),
                cancellationToken);
        }
    }

    private static IReadOnlyCollection<long> ResolveBillIds(
        Payment payment,
        IReadOnlyCollection<long> billIds)
    {
        long[] resolved = billIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return resolved.Length > 0 || payment.BillId <= 0
            ? resolved
            : [payment.BillId];
    }

    private static Dictionary<string, long> BuildPaymentRelatedIds(
        Payment payment,
        IEnumerable<long> billIds)
    {
        Dictionary<string, long> relatedIds = new()
        {
            ["paymentId"] = payment.Id,
            ["payerPersonId"] = payment.PayerPersonId
        };

        if (payment.BillingStatementId is long statementId)
        {
            relatedIds["billingStatementId"] = statementId;
        }

        long[] distinctBillIds = billIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (distinctBillIds.Length == 1)
        {
            relatedIds["billId"] = distinctBillIds[0];
        }

        return relatedIds;
    }
}
