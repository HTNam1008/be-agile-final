using Microsoft.Extensions.Logging;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.IGateway.Notifications;

public static class NotificationWriterBusinessFlowExtensions
{
    public static async Task<bool> CreateForBusinessFlowAsync(
        this INotificationWriter notificationWriter,
        NotificationCreateRequest request,
        ILogger logger,
        string businessOperation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Result<long> result = await notificationWriter.CreateAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                return true;
            }

            logger.LogError(
                "Notification creation failed for business operation {BusinessOperation}. RecipientUserAccountId={RecipientUserAccountId} NotificationTypeCode={NotificationTypeCode} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                businessOperation,
                request.RecipientUserAccountId,
                request.NotificationTypeCode,
                result.Error.Code,
                result.Error.Message);

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Notification creation threw for business operation {BusinessOperation}. RecipientUserAccountId={RecipientUserAccountId} NotificationTypeCode={NotificationTypeCode}",
                businessOperation,
                request.RecipientUserAccountId,
                request.NotificationTypeCode);

            return false;
        }
    }
}
