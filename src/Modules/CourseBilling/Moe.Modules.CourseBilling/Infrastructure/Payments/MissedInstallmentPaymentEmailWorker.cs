using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Payments;

internal sealed class MissedInstallmentPaymentEmailWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<MissedInstallmentPaymentEmailWorker> logger) : BackgroundService
{
    private readonly HashSet<long> _processedBillIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueNotificationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Missed installment email worker failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task SendDueNotificationsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        ICoursePaymentPlanGateway paymentPlans = scope.ServiceProvider.GetRequiredService<ICoursePaymentPlanGateway>();
        IEmailNotificationScheduler mailScheduler = scope.ServiceProvider.GetRequiredService<IEmailNotificationScheduler>();
        if (!mailScheduler.IsEnabled)
        {
            logger.LogInformation("Missed installment emails skipped because MailDelivery is disabled.");
            return;
        }

        IEmailBrandingProvider branding = scope.ServiceProvider.GetRequiredService<IEmailBrandingProvider>();

        DateOnly today = clock.TodayInSingapore();
        DateOnly missedDueDate = today.AddDays(-1);

        MissedInstallmentCandidate[] candidates = await (
                from bill in dbContext.Set<Bill>().AsNoTracking()
                join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                    on bill.CourseEnrollmentId equals enrollment.Id
                join course in dbContext.Set<Course>().AsNoTracking()
                    on enrollment.CourseId equals course.Id
                join person in dbContext.Set<Person>().AsNoTracking()
                    on enrollment.PersonId equals person.Id
                where bill.CurrentDueDate == missedDueDate
                    && bill.OutstandingAmount > 0m
                    && bill.BillStatusCode != BillStatusCodes.Paid
                    && bill.BillStatusCode != BillStatusCodes.Cancelled
                    && enrollment.CoursePaymentPlanId != null
                select new MissedInstallmentCandidate(
                    bill.Id,
                    enrollment.Id,
                    enrollment.PersonId,
                    enrollment.CoursePaymentPlanId!.Value,
                    string.IsNullOrWhiteSpace(person.OfficialFullName) ? "Student" : person.OfficialFullName,
                    course.CourseName,
                    bill.OutstandingAmount,
                    bill.CurrentDueDate))
            .ToArrayAsync(cancellationToken);

        foreach (MissedInstallmentCandidate candidate in candidates)
        {
            if (_processedBillIds.Contains(candidate.BillId))
            {
                continue;
            }

            CourseBillingPlan? plan = await paymentPlans.FindPlanAsync(
                candidate.CoursePaymentPlanId,
                cancellationToken);
            if (plan?.PlanTypeCode != "INSTALLMENT")
            {
                continue;
            }

            if (await EnqueueEmailAsync(candidate, mailScheduler, branding.AppName, branding.PaymentDashboardUrl, cancellationToken))
            {
                _processedBillIds.Add(candidate.BillId);
            }
        }
    }

    private async Task<bool> EnqueueEmailAsync(
        MissedInstallmentCandidate candidate,
        IEmailNotificationScheduler mailScheduler,
        string appName,
        string paymentDashboardUrl,
        CancellationToken cancellationToken)
    {
        string studentName = candidate.StudentName.Trim();
        string courseName = string.IsNullOrWhiteSpace(candidate.CourseName)
            ? "your course"
            : candidate.CourseName.Trim();
        string amountDisplay = EmailTemplateBranding.FormatMoney(candidate.OutstandingAmount);
        string dueDateDisplay = EmailTemplateBranding.FormatDate(candidate.DueDate);
        const string consequence = "Your course access or enrolment may remain restricted until payment is received.";

        const string subject = "Missed Installment Payment";
        string plainTextBody = string.Join(Environment.NewLine, [
            appName,
            "Missed installment payment",
            string.Empty,
            $"Hello {studentName}, your installment of {amountDisplay} for {courseName} due {dueDateDisplay} was not received.",
            consequence,
            string.Empty,
            $"Pay Now -> {paymentDashboardUrl}"
        ]);
        string htmlBody = BuildHtmlBody(studentName, amountDisplay, courseName, dueDateDisplay, consequence, appName, paymentDashboardUrl);

        return await mailScheduler.EnqueueForPersonAsync(
            "NOTI-11",
            candidate.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "Bill",
            candidate.BillId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private static string BuildHtmlBody(
        string studentName,
        string amountDisplay,
        string courseName,
        string dueDateDisplay,
        string consequence,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Missed installment payment", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your installment was not received by the due date.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Amount", amountDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Course", courseName);
        EmailTemplateBranding.AppendSummaryRow(builder, "Due Date", dueDateDisplay);
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">")
            .Append(WebUtility.HtmlEncode(consequence))
            .Append("</p>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "Pay Now");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName} after an installment due date passed.");
        return builder.ToString();
    }
    private sealed record MissedInstallmentCandidate(
        long BillId,
        long CourseEnrollmentId,
        long PersonId,
        long CoursePaymentPlanId,
        string StudentName,
        string CourseName,
        decimal OutstandingAmount,
        DateOnly DueDate);
}
