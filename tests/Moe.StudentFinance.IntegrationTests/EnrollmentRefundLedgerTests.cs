using Moe.Modules.FasPayment.Domain.Payments;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EnrollmentRefundLedgerTests
{
    [Fact]
    public void EnrollmentRefund_RecordsPolicyAndSourceAmounts()
    {
        var result = EnrollmentRefund.Create(
            courseEnrollmentId: 10,
            personId: 20,
            paidAmount: 150m,
            refundPercentage: 50m,
            refundAmount: 75m,
            educationAccountRefundAmount: 50m,
            onlineRefundAmount: 25m,
            policyPeriodCode: "DURING_COURSE",
            idempotencyKey: "ENROLLMENT-CANCEL:10",
            requestedByUserAccountId: 30,
            requestedAtUtc: DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Value.EducationAccountRefundAmount);
        Assert.Equal(25m, result.Value.OnlineRefundAmount);
        Assert.Equal(EnrollmentRefundStatusCodes.Pending, result.Value.RefundStatusCode);
    }

    [Fact]
    public void StripeRefundPart_StoresProviderRefund()
    {
        EnrollmentRefundPart part = EnrollmentRefundPart.Create(
            enrollmentRefundId: 1,
            paymentId: 2,
            paymentPartId: 3,
            refundMethodCode: EnrollmentRefundMethodCodes.Stripe,
            refundAmount: 25m,
            idempotencyKey: "ENROLLMENT-REFUND:1:STRIPE:2",
            createdAtUtc: DateTime.UtcNow);

        part.MarkStripeSucceeded("re_test", DateTime.UtcNow);

        Assert.Equal("re_test", part.ProviderRefundId);
        Assert.Equal(EnrollmentRefundStatusCodes.Succeeded, part.RefundStatusCode);
    }
}
