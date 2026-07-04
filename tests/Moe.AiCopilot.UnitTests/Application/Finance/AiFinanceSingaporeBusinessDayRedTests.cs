using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.CourseBilling.Application.BillingStatements;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Moe.AiCopilot.UnitTests.Application.Finance;

public sealed class AiFinanceSingaporeBusinessDayRedTests
{
    private static readonly DateTimeOffset SgtEarlyMorning =
        new(2026, 6, 30, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AiFinanceReader_uses_singapore_business_day_for_current_billing_period()
    {
        RecordingQueryDispatcher queries = new();
        AiFinanceReader reader = new(
            new FakeEducationAccountPaymentGateway(),
            queries,
            new FakeCurrentUser(),
            new TestClock(SgtEarlyMorning),
            NullLogger<AiFinanceReader>.Instance);

        await reader.GetSnapshotAsync(CancellationToken.None);

        queries.BillingStatementQuery.Should().NotBeNull();
        queries.BillingStatementQuery!.Year.Should().Be(2026);
        queries.BillingStatementQuery.Month.Should().Be(7);
    }

    private sealed class RecordingQueryDispatcher : IQueryDispatcher
    {
        public GetBillingStatementQuery? BillingStatementQuery { get; private set; }

        public Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        {
            if (query is GetBillingStatementQuery billingQuery)
            {
                BillingStatementQuery = billingQuery;
                object response = new BillingStatementResponse(
                    1,
                    billingQuery.Year,
                    billingQuery.Month,
                    "SGD",
                    0m,
                    0m,
                    0m,
                    "CURRENT",
                    []);
                return Task.FromResult(Result<TResponse>.Success((TResponse)response));
            }

            if (query is ListUserPaymentHistoryQuery)
            {
                object response = new PageResponse<UserPaymentHistoryResponse>([], 1, 5, 0);
                return Task.FromResult(Result<TResponse>.Success((TResponse)response));
            }

            throw new NotSupportedException(query.GetType().FullName);
        }
    }

    private sealed class FakeEducationAccountPaymentGateway : IEducationAccountPaymentGateway
    {
        public Task<EducationAccountPaymentBalance?> GetAvailableBalanceAsync(long personId, CancellationToken cancellationToken) =>
            Task.FromResult<EducationAccountPaymentBalance?>(new(1, 100m, 0m, 100m, "SGD"));

        public Task<long> ReserveAsync(long personId, long paymentPartId, decimal amount, DateTime expiresAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> CaptureAsync(long accountHoldId, long? actorLoginAccountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(long accountHoldId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> DebitImmediatelyAsync(long personId, long paymentPartId, decimal amount, long? actorLoginAccountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> CreditRefundAsync(long personId, long refundReferenceId, decimal amount, long? reversalOfTransactionId, string idempotencyKey, long? actorLoginAccountId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => 1;
        public long? OrganizationUnitId => 10;
        public IReadOnlyCollection<long> OrganizationUnitIds => [10];
        public IReadOnlyCollection<string> Roles => ["STUDENT"];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "ESERVICE";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => false;
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
