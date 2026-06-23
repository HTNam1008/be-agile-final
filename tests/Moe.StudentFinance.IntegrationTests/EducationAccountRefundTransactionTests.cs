using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountRefundTransactionTests
{
    [Fact]
    public void RefundTransaction_IsPositiveAndLinksOriginalDebit()
    {
        AccountTransaction transaction = AccountTransaction.Create(
            educationAccountId: 1,
            transactionTypeCode: "REFUND",
            amount: 75m,
            referenceTypeCode: "ENROLLMENT_REFUND",
            referenceId: 20,
            idempotencyKey: "ENROLLMENT-REFUND:20:EA",
            currentBalance: 25m,
            description: "Course enrollment refund",
            createdByUserId: 10,
            nowUtc: DateTime.UtcNow,
            reversalOfTransactionId: 15);

        Assert.Equal(75m, transaction.Amount);
        Assert.Equal(100m, transaction.BalanceAfter);
        Assert.Equal("REFUND", transaction.TransactionTypeCode);
        Assert.Equal(15, transaction.ReversalOfTransactionId);
    }
}
