using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.Notifications;

public sealed class FasInAppNotificationService(
    MoeDbContext dbContext,
    IStudentNotificationRecipientResolver recipientResolver,
    INotificationWriter notificationWriter,
    ILogger<FasInAppNotificationService> logger)
{
    public Task SendSubmissionAcknowledgementAsync(long applicationId, CancellationToken cancellationToken)
        => SendApplicationNotificationAsync(
            applicationId,
            NotificationTypeCode.FasSubmitted,
            "FAS Application Received",
            cancellationToken);

    public Task SendApplicationApprovedAsync(long applicationId, CancellationToken cancellationToken)
        => SendApplicationNotificationAsync(
            applicationId,
            NotificationTypeCode.FasEligible,
            "FAS Result: ELIGIBLE",
            cancellationToken);

    public Task SendApplicationRejectedAsync(
        long applicationId,
        string rejectionReasonCode,
        CancellationToken cancellationToken)
        => SendApplicationRejectedNotificationAsync(
            applicationId,
            rejectionReasonCode,
            cancellationToken);

    public Task SendPaymentSucceededAsync(long paymentId, CancellationToken cancellationToken)
        => SendPaymentNotificationAsync(
            paymentId,
            NotificationTypeCode.PaymentSuccess,
            "Payment Receipt",
            cancellationToken);

    public Task SendPaymentFailedAsync(long paymentId, CancellationToken cancellationToken)
        => SendPaymentNotificationAsync(
            paymentId,
            NotificationTypeCode.PaymentFail,
            "Payment Failed",
            cancellationToken);

    private async Task SendApplicationNotificationAsync(
        long applicationId,
        string notificationTypeCode,
        string fallbackTitle,
        CancellationToken cancellationToken)
    {
        FasApplication? application = await dbContext.Set<FasApplication>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            logger.LogWarning("FAS notification skipped because application {ApplicationId} was not found.", applicationId);
            return;
        }

        long? userAccountId = await recipientResolver.FindUserAccountIdByPersonIdAsync(
            application.AccountHolderPersonId,
            cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning("FAS notification skipped because no user account was found for person {PersonId}.", application.AccountHolderPersonId);
            return;
        }

        FasScheme? scheme = await dbContext.Set<FasScheme>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == application.FasSchemeId, cancellationToken);
        string schemeName = scheme?.Name ?? "FAS Scheme";
        string title = notificationTypeCode == NotificationTypeCode.FasSubmitted
            ? $"FAS Application Submitted: {application.ApplicationNo}"
            : $"FAS Application Approved: {application.ApplicationNo}";
        string body = notificationTypeCode == NotificationTypeCode.FasSubmitted
            ? $"New FAS application {application.ApplicationNo} for {schemeName} was submitted."
            : $"Your FAS application {application.ApplicationNo} for {schemeName} was approved.";

        await CreateAsync(userAccountId.Value, notificationTypeCode, title, body, cancellationToken);
    }

    private async Task SendApplicationRejectedNotificationAsync(
        long applicationId,
        string rejectionReasonCode,
        CancellationToken cancellationToken)
    {
        FasApplication? application = await dbContext.Set<FasApplication>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            logger.LogWarning("FAS rejection notification skipped because application {ApplicationId} was not found.", applicationId);
            return;
        }

        long? userAccountId = await recipientResolver.FindUserAccountIdByPersonIdAsync(
            application.AccountHolderPersonId,
            cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning("FAS rejection notification skipped because no user account was found for person {PersonId}.", application.AccountHolderPersonId);
            return;
        }

        FasScheme? scheme = await dbContext.Set<FasScheme>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == application.FasSchemeId, cancellationToken);
        string schemeName = scheme?.Name ?? "FAS Scheme";
        string title = $"FAS Application Rejected: {application.ApplicationNo}";
        string body = $"Your FAS application {application.ApplicationNo} for {schemeName} has been rejected. Reason: {rejectionReasonCode}.";

        await CreateAsync(userAccountId.Value, NotificationTypeCode.FasRejected, title, body, cancellationToken);
    }

    private async Task SendPaymentNotificationAsync(
        long paymentId,
        string notificationTypeCode,
        string fallbackTitle,
        CancellationToken cancellationToken)
    {
        Payment? payment = await dbContext.Set<Payment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            logger.LogWarning("FAS payment notification skipped because payment {PaymentId} was not found.", paymentId);
            return;
        }

        long? userAccountId = await recipientResolver.FindUserAccountIdByPersonIdAsync(
            payment.PayerPersonId,
            cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning("FAS payment notification skipped because no user account was found for person {PersonId}.", payment.PayerPersonId);
            return;
        }

        string body = notificationTypeCode == NotificationTypeCode.PaymentSuccess
            ? $"Successful Amount: {payment.SuccessfulAmount:N2}. Your payment was completed at {payment.CompletedAtUtc:yyyy-MM-dd HH:mm}."
            : "Your payment could not be completed. Please try again later.";
        string title = payment.ReceiptNumber is not null && notificationTypeCode == NotificationTypeCode.PaymentSuccess
            ? $"Payment Receipt: {payment.ReceiptNumber}"
            : fallbackTitle;

        await CreateAsync(userAccountId.Value, notificationTypeCode, title, body, cancellationToken);
    }

    private async Task CreateAsync(
        long userAccountId,
        string notificationTypeCode,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        await notificationWriter.CreateForBusinessFlowAsync(
            new NotificationCreateRequest(userAccountId, notificationTypeCode, title, body),
            logger,
            "FAS application notification",
            cancellationToken);
    }
}
