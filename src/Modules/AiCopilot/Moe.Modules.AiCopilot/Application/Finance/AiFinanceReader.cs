using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application.LegacyPayments;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.AiCopilot.Application.Finance;

/// <param name="BillCount">Number of bills with OutstandingAmount > 0</param>
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
        var billsResult = await queries.Send(new GetOutstandingBillsQuery(), ct);
        OutstandingBillsResponse bills = billsResult.IsSuccess
            ? billsResult.Value
            : new OutstandingBillsResponse(balance?.AvailableBalance ?? 0m, balance?.CurrencyCode ?? "SGD", []);
        var historyResult = await queries.Send(new ListUserPaymentHistoryQuery(), ct);
        IReadOnlyCollection<UserPaymentHistoryResponse> history = historyResult.IsSuccess ? historyResult.Value : [];

        AiOutstandingBill[] outstanding = bills.Bills
            .Where(x => x.OutstandingAmount > 0m)
            .OrderBy(x => x.DueDate)
            .Select(x => new AiOutstandingBill(x.BillId, x.BillNumber, x.CourseName, x.DueDate, x.OutstandingAmount, x.BillStatusCode))
            .ToArray();
        decimal total = outstanding.Sum(x => x.OutstandingAmount);
        decimal available = balance?.AvailableBalance ?? bills.EducationAccountBalance;
        return new AiFinanceSnapshot(
            balance?.CurrentBalance ?? bills.EducationAccountBalance,
            balance?.HeldBalance ?? 0m,
            available,
            total,
            available - total,
            balance?.CurrencyCode ?? bills.CurrencyCode,
            outstanding.Length,
            outstanding.FirstOrDefault()?.DueDate,
            outstanding,
            history.OrderByDescending(x => x.InitiatedAtUtc).Take(5)
                .Select(x => new AiPaymentHistoryItem(x.PaymentId, x.PaymentNumber, x.PaymentAmount,
                    x.PaymentStatusCode, x.InitiatedAtUtc, x.Refunds.Sum(r => r.Amount))).ToArray());
    }
}
