using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class TopUpTransactionTests
{
    private readonly DateTime _utcNow = new(2026, 6, 17, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_Create_Pending_Transaction()
    {
        TopUpTransaction transaction = CreateTransaction(amount: 250m);

        transaction.TopUpRunId.Should().Be(42);
        transaction.EducationAccountId.Should().Be(100);
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Pending);
        transaction.IdempotencyKey.Should().Be("topup:42:100");
        transaction.Amount.Should().Be(250m);
        transaction.CreatedAtUtc.Should().Be(_utcNow);
        transaction.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Should_Generate_Correct_Idempotency_Key()
    {
        TopUpTransaction transaction = CreateTransaction(runId: 42, accountId: 100);

        transaction.IdempotencyKey.Should().Be("topup:42:100");
    }

    [Fact]
    public void Should_Complete_Pending_Transaction()
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Complete(777, _utcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Completed);
        transaction.AccountTransactionId.Should().Be(777);
        transaction.CompletedAtUtc.Should().Be(_utcNow.AddMinutes(1));
    }

    [Fact]
    public void Should_Fail_Pending_Transaction()
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Fail("Account not active", _utcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Failed);
        transaction.Amount.Should().Be(250m);
        transaction.Reason.Should().Be("Account not active");
        transaction.CompletedAtUtc.Should().Be(_utcNow.AddMinutes(1));
    }

    [Fact]
    public void Should_Skip_Pending_Transaction()
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Skip("Account closed", _utcNow.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Skipped);
        transaction.Amount.Should().Be(250m);
        transaction.Reason.Should().Be("Account closed");
        transaction.CompletedAtUtc.Should().Be(_utcNow.AddMinutes(1));
    }

    [Fact]
    public void Should_Reject_Complete_On_Already_Completed()
    {
        TopUpTransaction transaction = CreateTransaction();
        transaction.Complete(777, _utcNow).IsSuccess.Should().BeTrue();

        var result = transaction.Complete(778, _utcNow.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.TransactionIsTerminal);
    }

    [Fact]
    public void Should_Reject_Fail_On_Already_Completed()
    {
        TopUpTransaction transaction = CreateTransaction();
        transaction.Complete(777, _utcNow).IsSuccess.Should().BeTrue();

        var result = transaction.Fail("Account not active", _utcNow.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.TransactionIsTerminal);
    }

    [Fact]
    public void Should_Reject_Skip_On_Already_Failed()
    {
        TopUpTransaction transaction = CreateTransaction();
        transaction.Fail("Account not active", _utcNow).IsSuccess.Should().BeTrue();

        var result = transaction.Skip("Account closed", _utcNow.AddMinutes(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.TransactionIsTerminal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Reject_Complete_With_Invalid_AccountTransactionId(long accountTransactionId)
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Complete(accountTransactionId, _utcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.InvalidAccountTransactionReference);
        transaction.TransactionStatusCode.Should().Be(TopUpTransactionStatusCodes.Pending);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_Reject_Fail_With_Empty_Reason(string reason)
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Fail(reason, _utcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.TransactionReasonRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_Reject_Skip_With_Empty_Reason(string reason)
    {
        TopUpTransaction transaction = CreateTransaction();

        var result = transaction.Skip(reason, _utcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.TransactionReasonRequired);
    }

    [Fact]
    public void Should_Preserve_Amount_On_Completion()
    {
        TopUpTransaction transaction = CreateTransaction(amount: 500m);

        transaction.Complete(777, _utcNow).IsSuccess.Should().BeTrue();

        transaction.Amount.Should().Be(500m);
    }

    [Fact]
    public void Should_Preserve_Amount_On_Failure()
    {
        TopUpTransaction transaction = CreateTransaction(amount: 500m);

        transaction.Fail("Account not active", _utcNow).IsSuccess.Should().BeTrue();

        transaction.Amount.Should().Be(500m);
    }

    [Fact]
    public void Should_Prevent_Direct_Modification_Of_Transaction_State()
    {
        string[] propertyNames =
        [
            nameof(TopUpTransaction.TransactionStatusCode),
            nameof(TopUpTransaction.Amount),
            nameof(TopUpTransaction.AccountTransactionId),
            nameof(TopUpTransaction.Reason),
            nameof(TopUpTransaction.CompletedAtUtc)
        ];

        foreach (string propertyName in propertyNames)
        {
            typeof(TopUpTransaction)
                .GetProperty(propertyName)!
                .SetMethod!
                .IsPublic
                .Should()
                .BeFalse($"{propertyName} must not have a public setter");
        }
    }

    private TopUpTransaction CreateTransaction(
        long runId = 42,
        long accountId = 100,
        decimal amount = 250m)
    {
        return TopUpTransaction.Create(runId, accountId, amount, _utcNow);
    }
}
