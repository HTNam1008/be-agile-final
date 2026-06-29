using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Application.BillingStatements;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.AiCopilot.Application.Finance;

/// <param name="BillCount">Number of bills with OutstandingAmount &gt; 0</param>
/// <param name="TotalOutstanding">Sum of OutstandingAmount across those bills</param>
public sealed record AiFinanceSnapshot(
    decimal CurrentBalance,
    decimal HeldBalance,
    decimal AvailableBalance,
    decimal TotalOutstanding,
    decimal NetAvailable,
    string CurrencyCode,
    int BillCount,
    DateOnly? NearestDueDate,
    IReadOnlyCollection<AiOutstandingBill> Bills,
    IReadOnlyCollection<AiPaymentHistoryItem> RecentPayments);

public sealed record AiOutstandingBill(long BillId, string BillNumber, string Description, DateOnly DueDate,
    decimal OutstandingAmount, string StatusCode);
public sealed record AiPaymentHistoryItem(long PaymentId, string PaymentNumber, decimal Amount, string StatusCode,
    DateTime InitiatedAtUtc, decimal RefundedAmount);

public sealed class AiFinanceReader(
    IEducationAccountPaymentGateway accounts,
    IQueryDispatcher queries,
    ICurrentUser currentUser)
{
    public async Task<AiFinanceSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);

        DateTime utcNow = DateTime.UtcNow;
        var statementResult = await queries.Send(
            new GetBillingStatementQuery(utcNow.Year, utcNow.Month), ct);
        BillingStatementResponse? statement = statementResult.IsSuccess ? statementResult.Value : null;

        var historyResult = await queries.Send(new ListUserPaymentHistoryQuery(1, 5), ct);
        IReadOnlyCollection<UserPaymentHistoryResponse> history = historyResult.IsSuccess ? historyResult.Value.Items : [];

        AiOutstandingBill[] outstanding = statement?.Items
            .Where(x => x.OutstandingAmount > 0m)
            .OrderBy(x => x.CurrentDueDate)
            .Select(x => new AiOutstandingBill(
                x.BillId,
                $"BILL-{x.BillId}",
                x.CourseName,
                x.CurrentDueDate,
                x.OutstandingAmount,
                x.BillStatusCode))
            .ToArray() ?? [];

        decimal total = outstanding.Sum(x => x.OutstandingAmount);
        string currencyCode = balance?.CurrencyCode ?? statement?.CurrencyCode ?? "SGD";
        decimal available = balance?.AvailableBalance ?? 0m;

        return new AiFinanceSnapshot(
            balance?.CurrentBalance ?? 0m,
            balance?.HeldBalance ?? 0m,
            available,
            total,
            available - total,
            currencyCode,
            outstanding.Length,
            outstanding.FirstOrDefault()?.DueDate,
            outstanding,
            history.OrderByDescending(x => x.InitiatedAtUtc)
                .Select(x => new AiPaymentHistoryItem(x.PaymentId, x.PaymentNumber, x.PaymentAmount,
                    x.PaymentStatusCode, x.InitiatedAtUtc, x.Refunds.Sum(r => r.Amount))).ToArray());
    }
}
