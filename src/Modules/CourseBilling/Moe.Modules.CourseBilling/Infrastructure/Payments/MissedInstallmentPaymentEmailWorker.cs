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
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Payments;

internal sealed class MissedInstallmentPaymentEmailWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<MissedInstallmentPaymentEmailWorker> logger) : BackgroundService
{
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";

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

    private async Task SendDueNotificationsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IEmailDeliverySwitch mailSwitch = scope.ServiceProvider.GetRequiredService<IEmailDeliverySwitch>();
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation("Missed installment emails skipped because MailDelivery is disabled.");
            return;
        }

        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        ICoursePaymentPlanGateway paymentPlans = scope.ServiceProvider.GetRequiredService<ICoursePaymentPlanGateway>();
        IEmailRecipientResolver recipientResolver = scope.ServiceProvider.GetRequiredService<IEmailRecipientResolver>();
        IEmailDeliveryGateway mailGateway = scope.ServiceProvider.GetRequiredService<IEmailDeliveryGateway>();

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
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
            CourseBillingPlan? plan = await paymentPlans.FindPlanAsync(
                candidate.CoursePaymentPlanId,
                cancellationToken);
            if (plan?.PlanTypeCode != "INSTALLMENT")
            {
                continue;
            }

            await SendEmailAsync(candidate, recipientResolver, mailGateway, cancellationToken);
        }
    }

    private async Task SendEmailAsync(
        MissedInstallmentCandidate candidate,
        IEmailRecipientResolver recipientResolver,
        IEmailDeliveryGateway mailGateway,
        CancellationToken cancellationToken)
    {
        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(candidate.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Missed installment email recipient resolution failed. PersonId={PersonId} BillId={BillId}", candidate.PersonId, candidate.BillId);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Missed installment email skipped because no valid recipient was found. PersonId={PersonId} BillId={BillId}", candidate.PersonId, candidate.BillId);
            return;
        }

        string studentName = candidate.StudentName.Trim();
        string courseName = string.IsNullOrWhiteSpace(candidate.CourseName)
            ? "your course"
            : candidate.CourseName.Trim();
        string amountDisplay = $"SGD {candidate.OutstandingAmount.ToString("N2", CultureInfo.InvariantCulture)}";
        string dueDateDisplay = candidate.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        const string consequence = "Your course access or enrolment may remain restricted until payment is received.";

        const string subject = "Missed Installment Payment";
        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Missed installment payment",
            string.Empty,
            $"Hello {studentName}, your installment of {amountDisplay} for {courseName} due {dueDateDisplay} was not received.",
            consequence,
            string.Empty,
            $"Pay Now -> {PaymentDashboardUrl}"
        ]);
        string htmlBody = BuildHtmlBody(studentName, amountDisplay, courseName, dueDateDisplay, consequence);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Missed installment email failed. BillId={BillId} ErrorCode={ErrorCode}",
                    candidate.BillId,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Missed installment email threw an exception. BillId={BillId}", candidate.BillId);
        }
    }

    private static string BuildHtmlBody(
        string studentName,
        string amountDisplay,
        string courseName,
        string dueDateDisplay,
        string consequence)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Missed installment payment");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your installment was not received by the due date.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Amount", amountDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course", courseName, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Due Date", dueDateDisplay, "#f8fafc", "#334155");
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">")
            .Append(WebUtility.HtmlEncode(consequence))
            .Append("</p>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, "Pay Now");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after an installment due date passed.</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
        return builder.ToString();
    }

    private static void AppendSummaryRow(
        StringBuilder builder,
        string label,
        string value,
        string backgroundColor,
        string valueColor)
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(backgroundColor)
            .Append("\" style=\"background-color:")
            .Append(backgroundColor)
            .Append(";padding:14px 16px;border-bottom:8px solid #ffffff;\">");
        builder.Append("<div style=\"font-size:12px;line-height:18px;color:#64748b;text-transform:uppercase;font-weight:bold;letter-spacing:1px;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</div>");
        builder.Append("<div style=\"font-size:20px;line-height:28px;color:")
            .Append(valueColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</div></td></tr>");
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
