using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class PaymentQueryHandler(
    AiFinanceReader finance,
    IKnowledgeRetriever knowledge)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiHandlerResult> HandlePaymentAsync(AiChatRequest request, CancellationToken ct)
    {
        AiFinanceSnapshot snapshot = await finance.GetSnapshotAsync(ct);
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, "PAYMENT");
        string intent = request.Message.ToUpperInvariant();
        var sg = CultureInfo.GetCultureInfo("en-SG");
        string ccy(decimal v) => v.ToString("C", sg);

        if (intent.Contains("HISTORY") || intent.Contains("PAID") || intent.Contains("REFUND"))
        {
            string historyText = snapshot.RecentPayments.Count == 0
                ? "I could not find recent payment records for your account yet. Refunds depend on the payment type and conditions of the original transaction. You can check the Bills & payments page for current outstanding charges."
                : $"You have {snapshot.RecentPayments.Count} recent payment record{(snapshot.RecentPayments.Count == 1 ? "" : "s")}. Refunds are processed based on the original payment method and may take 5-14 business days. Contact your school or Admin Center for refund eligibility.";
            return new AiHandlerResult(historyText, "PAYMENT", Grounding(sources),
                [new("PAYMENT_HISTORY", snapshot.RecentPayments)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")]);
        }

        if (intent.Contains("WITHDRAW"))
        {
            string withdrawText = "To withdraw from a course, start by reviewing the withdrawal policy on the Bills & payments page. Withdrawals may affect your outstanding charges and Education Account balance. Contact your school for eligibility, deadlines, and any supporting documents needed. You can also reach the Admin Center for further assistance.";
            return new AiHandlerResult(withdrawText, "PAYMENT", Grounding(sources), [],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")]);
        }

        if (intent.Contains("EDUCATION ACCOUNT") && Regex.IsMatch(intent, @"\b(PAY|USE|USED|FOR|COVER)\b", RegexOptions.IgnoreCase))
        {
            string accountUseText = snapshot.TotalOutstanding <= 0m
                ? $"You have {ccy(snapshot.AvailableBalance)} available in your Education Account. You can use it for eligible course bills and supported student-finance charges when they are issued. You do not have an outstanding bill right now, so there is nothing to pay from it at the moment."
                : $"You have {ccy(snapshot.AvailableBalance)} available in your Education Account. You can use it for eligible course bills and supported student-finance charges. You currently have {ccy(snapshot.TotalOutstanding)} outstanding; open Bills & payments to review what can be paid now.";
            return new AiHandlerResult(accountUseText, "PAYMENT", Grounding(sources),
                [new("FINANCE_SUMMARY", snapshot)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")]);
        }

        if (Regex.IsMatch(intent, @"\b(HOW|METHOD|OPTION|OPTIONS)\b", RegexOptions.IgnoreCase) && intent.Contains("PAY"))
        {
            string paymentText = snapshot.TotalOutstanding <= 0m
                ? "You do not have an outstanding bill to pay right now. When a bill is due, you can usually pay with available Education Account funds, online payment, or split payment where supported."
                : PaymentOptionsText(snapshot);
            return new AiHandlerResult(paymentText, "PAYMENT", Grounding(sources),
                [new("FINANCE_SUMMARY", snapshot)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")]);
        }

        if (intent.Contains("BILL") || intent.Contains("OUTSTANDING") || intent.Contains("DUE"))
        {
            string billText = snapshot.BillCount == 0
                ? "You have no outstanding course bills right now. Check the Bills & payments page for your payment history."
                : $"You have {snapshot.BillCount} outstanding course bill{(snapshot.BillCount == 1 ? "" : "s")} totalling {ccy(snapshot.TotalOutstanding)}. View and pay these on the Bills & payments page.";
            return new AiHandlerResult(billText, "PAYMENT", Grounding(sources),
                [new("OUTSTANDING_BILLS", snapshot.Bills)], [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")]);
        }

        string paymentStatus = snapshot.TotalOutstanding <= 0
            ? "Nothing is due right now."
            : "Review the available balance and outstanding charges before paying.";
        string text = $"Your live Education Account summary is below, including available balance, outstanding charges, and net available amount.\n\n{paymentStatus}\n\nUse the actions below to open the exact Bills & payments or Education Account view.";
        AiCard card = new("FINANCE_SUMMARY", snapshot);
        AiAction[] actions = [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")];
        return new AiHandlerResult(text, "PAYMENT", Grounding(sources), [card], actions);
    }

    private static string PaymentOptionsText(AiFinanceSnapshot snapshot)
    {
        if (snapshot.TotalOutstanding <= 0m) return "Nothing is due right now.";
        var info = CultureInfo.GetCultureInfo("en-SG");
        string f(decimal v) => v.ToString("C", info);
        if (snapshot.AvailableBalance >= snapshot.TotalOutstanding)
            return "Your Education Account balance covers the outstanding amount. Review the bill details before paying, and settle any charges before enrolling in new courses.";
        if (snapshot.AvailableBalance > 0m)
        {
            decimal remainder = snapshot.TotalOutstanding - snapshot.AvailableBalance;
            return $"Your Education Account covers part of the outstanding amount but is short by {f(remainder)}. Use split payment or another online method for the remainder where supported.";
        }
        return "Your Education Account does not have available funds for this amount. Use another online payment method where supported.";
    }

    private static AiGrounding Grounding(IReadOnlyList<KnowledgeResult> sources) => new(sources.Count > 0, sources.Select(x => x.Citation).ToArray());
}
