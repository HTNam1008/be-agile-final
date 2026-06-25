using FluentAssertions;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class StatementBillSelectionTests
{
    [Fact]
    public void Select_OnNoBillIds_ReturnsAllBills()
    {
        PayableStatement statement = CreateStatement();

        var result = StatementBillSelection.Select(statement, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.BillId).Should().Equal(101, 102);
    }

    [Fact]
    public void Select_OnSpecificBillIds_ReturnsOnlyRequestedBills()
    {
        PayableStatement statement = CreateStatement();

        var result = StatementBillSelection.Select(statement, [102]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().OutstandingAmount.Should().Be(75m);
    }

    [Fact]
    public void Select_OnBillOutsideStatement_ReturnsFailure()
    {
        PayableStatement statement = CreateStatement();

        var result = StatementBillSelection.Select(statement, [999]);

        result.IsFailure.Should().BeTrue();
    }

    private static PayableStatement CreateStatement()
        => new(
            BillingStatementId: 10,
            PersonId: 5001,
            OutstandingAmount: 125m,
            CurrencyCode: "SGD",
            Bills:
            [
                new(
                    BillingStatementItemId: 1,
                    BillId: 101,
                    OutstandingAmount: 50m,
                    CurrentDueDate: new DateOnly(2026, 6, 1),
                    OriginalDueDate: new DateOnly(2026, 6, 1),
                    IsInstallment: false),
                new(
                    BillingStatementItemId: 2,
                    BillId: 102,
                    OutstandingAmount: 75m,
                    CurrentDueDate: new DateOnly(2026, 6, 2),
                    OriginalDueDate: new DateOnly(2026, 6, 2),
                    IsInstallment: true)
            ]);
}
