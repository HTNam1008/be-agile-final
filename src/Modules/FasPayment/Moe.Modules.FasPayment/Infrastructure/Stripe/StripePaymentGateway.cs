using System.Text.Json;
using Microsoft.Extensions.Options;
using Moe.Modules.FasPayment.IGateway.Payments;
using Stripe;
using Stripe.Checkout;

namespace Moe.Modules.FasPayment.Infrastructure.Stripe;

internal sealed class StripePaymentGateway(IOptions<StripePaymentOptions> options) : IStripePaymentGateway
{
    public async Task<StripeCheckoutGatewayResult> CreateCheckoutAsync(
        StripeCheckoutGatewayRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateCheckoutCoreAsync(request, cancellationToken);
        }
        catch (StripeException)
        {
            throw new PaymentProviderUnavailableException();
        }
    }

    private async Task<StripeCheckoutGatewayResult> CreateCheckoutCoreAsync(
        StripeCheckoutGatewayRequest request,
        CancellationToken cancellationToken)
    {
        StripePaymentOptions configuration = RequiredConfiguration();
        StripeConfiguration.ApiKey = configuration.SecretKey;

        string? priceId = request.LineItems is { Count: > 0 }
            ? request.ProviderPriceId
            : request.ProviderPriceId ?? await CreatePriceAsync(request, cancellationToken);
        var metadata = new Dictionary<string, string>
        {
            ["checkoutId"] = request.CheckoutId.ToString(),
            ["billId"] = request.BillId.ToString(),
            ["courseId"] = request.CourseId.ToString()
        };
        if (request.PaymentId is long paymentId)
            metadata["paymentId"] = paymentId.ToString();
        if (request.BillingStatementId is long billingStatementId)
            metadata["billingStatementId"] = billingStatementId.ToString();
        var session = await new SessionService().CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = configuration.SuccessUrl.Replace("{CHECKOUT_ID}", request.CheckoutId.ToString(), StringComparison.Ordinal),
                CancelUrl = configuration.CancelUrl.Replace("{CHECKOUT_ID}", request.CheckoutId.ToString(), StringComparison.Ordinal),
                PaymentMethodTypes = ["card", "paynow", "alipay"],
                ExpiresAt = request.ExpiresAtUtc,
                LineItems = BuildLineItems(request, priceId),
                Metadata = metadata,
                InvoiceCreation = new SessionInvoiceCreationOptions
                {
                    Enabled = true,
                    InvoiceData = new SessionInvoiceCreationInvoiceDataOptions
                    {
                        Description = request.BillingStatementId is long statementId
                            ? $"Monthly billing statement {statementId}"
                            : request.CourseName,
                        Metadata = metadata
                    }
                },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = metadata
                }
            },
            new RequestOptions { IdempotencyKey = request.IdempotencyKey },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
            throw new InvalidOperationException("Stripe did not return a Checkout URL.");
        return new(session.Id, priceId ?? "line_items", session.Url, session.ExpiresAt);
    }

    public async Task<StripePaymentEvidenceGatewayResult> GetPaymentEvidenceAsync(
        string? providerCheckoutSessionId,
        string? providerPaymentIntentId,
        string? providerInvoiceId,
        string? providerChargeId,
        CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = RequiredConfiguration().SecretKey;

            string? invoiceId = providerInvoiceId;
            string? paymentIntentId = providerPaymentIntentId;
            string? chargeId = providerChargeId;
            if (!string.IsNullOrWhiteSpace(providerCheckoutSessionId))
            {
                var session = await new SessionService().GetAsync(
                    providerCheckoutSessionId,
                    cancellationToken: cancellationToken);
                invoiceId ??= session.InvoiceId;
                paymentIntentId ??= session.PaymentIntentId;
            }

            string? hostedInvoiceUrl = null;
            string? invoicePdfUrl = null;
            if (!string.IsNullOrWhiteSpace(invoiceId))
            {
                var invoice = await new InvoiceService().GetAsync(invoiceId, cancellationToken: cancellationToken);
                hostedInvoiceUrl = invoice.HostedInvoiceUrl;
                invoicePdfUrl = invoice.InvoicePdf;
            }

            string? receiptUrl = null;
            if (!string.IsNullOrWhiteSpace(paymentIntentId))
            {
                var paymentIntent = await new PaymentIntentService().GetAsync(
                    paymentIntentId,
                    new PaymentIntentGetOptions
                    {
                        Expand = ["latest_charge"]
                    },
                    cancellationToken: cancellationToken);
                chargeId ??= paymentIntent.LatestChargeId;
                receiptUrl = paymentIntent.LatestCharge?.ReceiptUrl;
            }

            if (receiptUrl is null && !string.IsNullOrWhiteSpace(chargeId))
            {
                var charge = await new ChargeService().GetAsync(chargeId, cancellationToken: cancellationToken);
                receiptUrl = charge.ReceiptUrl;
            }

            return new StripePaymentEvidenceGatewayResult(hostedInvoiceUrl, invoicePdfUrl, receiptUrl);
        }
        catch (StripeException)
        {
            throw new PaymentProviderUnavailableException();
        }
    }

    public async Task<StripeScheduleGatewayResult> AttachFiniteScheduleAsync(
        string providerSubscriptionId,
        string providerPriceId,
        int installmentCount,
        CancellationToken cancellationToken)
    {
        StripeConfiguration.ApiKey = RequiredConfiguration().SecretKey;
        var schedules = new SubscriptionScheduleService();
        var schedule = await schedules.CreateAsync(
            new SubscriptionScheduleCreateOptions { FromSubscription = providerSubscriptionId },
            cancellationToken: cancellationToken);
        schedule = await schedules.UpdateAsync(
            schedule.Id,
            new SubscriptionScheduleUpdateOptions
            {
                EndBehavior = "cancel",
                Phases =
                [
                    new SubscriptionSchedulePhaseOptions
                    {
                        Duration = new SubscriptionSchedulePhaseDurationOptions
                        {
                            Interval = "month",
                            IntervalCount = installmentCount
                        },
                        Items = [new SubscriptionSchedulePhaseItemOptions { Price = providerPriceId, Quantity = 1 }]
                    }
                ]
            },
            cancellationToken: cancellationToken);
        return new(schedule.Id);
    }

    private static List<SessionLineItemOptions> BuildLineItems(
        StripeCheckoutGatewayRequest request,
        string? priceId)
    {
        if (request.LineItems is { Count: > 0 })
        {
            return request.LineItems
                .Where(item => item.AmountMinor > 0)
                .Select(item => new SessionLineItemOptions
                {
                    Quantity = item.Quantity <= 0 ? 1 : item.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.CurrencyCode.ToLowerInvariant(),
                        UnitAmount = item.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Name,
                            Description = item.Description
                        }
                    }
                })
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(priceId))
            throw new InvalidOperationException("Stripe Checkout requires either a price or line items.");

        return [new SessionLineItemOptions { Price = priceId, Quantity = 1 }];
    }

    public async Task ExpireCheckoutAsync(
        string providerSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = RequiredConfiguration().SecretKey;
            await new SessionService().ExpireAsync(
                providerSessionId,
                cancellationToken: cancellationToken);
        }
        catch (StripeException)
        {
            throw new PaymentProviderUnavailableException();
        }
    }

    public ParsedPaymentWebhook ParseWebhook(string payload, string signatureHeader)
    {
        try
        {
            return ParseVerifiedWebhook(payload, signatureHeader);
        }
        catch (Exception exception) when (exception is StripeException or JsonException or FormatException)
        {
            throw new InvalidPaymentWebhookException();
        }
    }

    public async Task<StripeRefundGatewayResult> CreateRefundAsync(
        string idempotencyKey,
        string providerChargeId,
        long amountMinor,
        CancellationToken cancellationToken)
    {
        try
        {
            StripeConfiguration.ApiKey = RequiredConfiguration().SecretKey;
            RefundCreateOptions options = providerChargeId.StartsWith("pi_", StringComparison.Ordinal)
                ? new RefundCreateOptions { PaymentIntent = providerChargeId, Amount = amountMinor }
                : new RefundCreateOptions { Charge = providerChargeId, Amount = amountMinor };
            var refund = await new RefundService().CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = idempotencyKey },
                cancellationToken);
            return new StripeRefundGatewayResult(refund.Id);
        }
        catch (StripeException)
        {
            throw new PaymentProviderUnavailableException();
        }
    }

    private ParsedPaymentWebhook ParseVerifiedWebhook(string payload, string signatureHeader)
    {
        StripePaymentOptions configuration = RequiredConfiguration();
        // Stripe CLI can forward events using the account's API version, which
        // may differ from the version bundled with Stripe.NET. Signature
        // verification remains enabled; only the strict version equality check
        // is disabled because this parser reads the raw JSON contract below.
        EventUtility.ConstructEvent(
            payload,
            signatureHeader,
            configuration.WebhookSecret,
            throwOnApiVersionMismatch: false);
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        string eventId = RequiredText(root, "id");
        string eventType = RequiredText(root, "type");
        DateTime createdAtUtc = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("created").GetInt64()).UtcDateTime;
        JsonElement data = root.GetProperty("data").GetProperty("object");

        PaymentWebhookKind kind = eventType switch
        {
            "checkout.session.completed" => PaymentWebhookKind.CheckoutCompleted,
            "checkout.session.expired" => PaymentWebhookKind.CheckoutExpired,
            "checkout.session.async_payment_succeeded" => PaymentWebhookKind.PaymentSucceeded,
            "checkout.session.async_payment_failed" => PaymentWebhookKind.PaymentFailed,
            "payment_intent.succeeded" => PaymentWebhookKind.PaymentSucceeded,
            "payment_intent.payment_failed" => PaymentWebhookKind.PaymentFailed,
            "invoice.paid" => PaymentWebhookKind.InvoicePaid,
            "invoice.payment_failed" => PaymentWebhookKind.InvoicePaymentFailed,
            "customer.subscription.deleted" => PaymentWebhookKind.SubscriptionDeleted,
            "charge.refunded" => PaymentWebhookKind.ChargeRefunded,
            _ => PaymentWebhookKind.Ignored
        };

        return new ParsedPaymentWebhook(
            eventId,
            eventType,
            kind,
            createdAtUtc,
            kind is PaymentWebhookKind.Ignored or PaymentWebhookKind.ChargeRefunded ? 0 : ReadCheckoutId(data),
            OptionalText(data, "id", eventType.StartsWith("checkout.session", StringComparison.Ordinal)),
            OptionalText(data, "payment_intent") ?? (eventType.StartsWith("payment_intent", StringComparison.Ordinal) ? OptionalText(data, "id") : null),
            eventType.StartsWith("invoice", StringComparison.Ordinal) ? OptionalText(data, "id") : null,
            kind == PaymentWebhookKind.ChargeRefunded ? OptionalText(data, "id") : OptionalText(data, "latest_charge") ?? OptionalText(data, "charge"),
            OptionalText(data, "subscription") ?? NestedText(data, "parent", "subscription_details", "subscription"),
            ReadAmount(data),
            OptionalText(data, "currency") ?? "sgd");
    }

    private async Task<string> CreatePriceAsync(
        StripeCheckoutGatewayRequest request,
        CancellationToken cancellationToken)
    {
        var product = await new ProductService().CreateAsync(
            new ProductCreateOptions
            {
                Name = request.CourseName,
                Metadata = new Dictionary<string, string> { ["courseId"] = request.CourseId.ToString() }
            },
            new RequestOptions { IdempotencyKey = $"{request.IdempotencyKey}:product" },
            cancellationToken);
        var price = await new PriceService().CreateAsync(
            new PriceCreateOptions
            {
                Product = product.Id,
                Currency = request.CurrencyCode.ToLowerInvariant(),
                UnitAmount = request.UnitAmountMinor,
                Recurring = null
            },
            new RequestOptions { IdempotencyKey = $"{request.IdempotencyKey}:price" },
            cancellationToken);
        return price.Id;
    }

    private StripePaymentOptions RequiredConfiguration()
    {
        StripePaymentOptions value = options.Value;
        if (string.IsNullOrWhiteSpace(value.SecretKey) ||
            string.IsNullOrWhiteSpace(value.WebhookSecret) ||
            string.IsNullOrWhiteSpace(value.SuccessUrl) ||
            string.IsNullOrWhiteSpace(value.CancelUrl))
        {
            throw new InvalidOperationException("Stripe configuration must be supplied through environment variables or a secret store.");
        }
        return value;
    }

    private static long ReadCheckoutId(JsonElement element)
    {
        string? value = Metadata(element, "checkoutId")
            ?? NestedText(element, "parent", "subscription_details", "metadata", "checkoutId")
            ?? NestedText(element, "subscription_details", "metadata", "checkoutId");
        return long.TryParse(value, out long checkoutId) && checkoutId > 0
            ? checkoutId
            : throw new JsonException("Stripe checkout metadata is missing.");
    }

    private static long ReadAmount(JsonElement element)
    {
        foreach (string name in new[] { "amount_refunded", "amount_received", "amount_paid", "amount_total", "amount_due", "amount" })
            if (element.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long amount)) return amount;
        return 0;
    }

    private static string RequiredText(JsonElement element, string name)
        => OptionalText(element, name) ?? throw new JsonException($"{name} is missing.");

    private static string? OptionalText(JsonElement element, string name, bool when = true)
        => when &&
           element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out JsonElement value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Metadata(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty("metadata", out JsonElement metadata) &&
           metadata.ValueKind == JsonValueKind.Object &&
           metadata.TryGetProperty(name, out JsonElement value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NestedText(JsonElement element, params string[] path)
    {
        foreach (string part in path)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(part, out element) ||
                element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
