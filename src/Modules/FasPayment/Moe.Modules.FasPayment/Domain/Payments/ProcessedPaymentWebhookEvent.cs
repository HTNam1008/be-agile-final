using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Payments;

internal sealed class ProcessedPaymentWebhookEvent : Entity<long>
{
    private ProcessedPaymentWebhookEvent() : base(0) { }

    public ProcessedPaymentWebhookEvent(string providerEventId, string eventType, DateTime receivedAtUtc) : base(0)
    {
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        ProcessingStatusCode = WebhookProcessingStatusCodes.Processing;
        ReceivedAtUtc = receivedAtUtc;
        AttemptCount = 1;
    }

    public string ProviderEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string ProcessingStatusCode { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessingStatusCode = WebhookProcessingStatusCodes.Processed;
        ProcessedAtUtc = processedAtUtc;
        FailureMessage = null;
    }

    public void MarkFailed(string message)
    {
        ProcessingStatusCode = WebhookProcessingStatusCodes.Failed;
        FailureMessage = message.Length <= 1000 ? message : message[..1000];
    }
}

public static class WebhookProcessingStatusCodes
{
    public const string Processing = "PROCESSING";
    public const string Processed = "PROCESSED";
    public const string Failed = "FAILED";
}
