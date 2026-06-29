using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Infrastructure.Notifications;

public sealed class CreateStudentMailNotificationMiddleware(
    RequestDelegate next,
    IEmailDeliveryGateway mailGateway,
    IOptions<MailDeliveryOptions> options,
    IClock clock,
    ILogger<CreateStudentMailNotificationMiddleware> logger)
{
    internal const string NotificationRecipient = "tphatdn1@gmail.com";
    private const string ObservedPath = "/api/admin/v1/students";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsObservedRequest(context.Request))
        {
            await next(context);
            return;
        }

        Stream originalBody = context.Response.Body;
        await using MemoryStream capturedBody = new();
        context.Response.Body = capturedBody;

        try
        {
            await next(context);

            capturedBody.Position = 0;
            string responseBody = await new StreamReader(capturedBody, Encoding.UTF8, leaveOpen: true)
                .ReadToEndAsync(context.RequestAborted);

            await SendNotificationAsync(context, responseBody);

            capturedBody.Position = 0;
            await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsObservedRequest(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
            && request.Path.Equals(ObservedPath, StringComparison.OrdinalIgnoreCase);

    private async Task SendNotificationAsync(HttpContext context, string responseBody)
    {
        CreateStudentNotification notification = CreateStudentNotification.FromResponse(
            options.Value.AppName,
            clock.UtcNow.UtcDateTime,
            context.Response.StatusCode,
            context.TraceIdentifier,
            responseBody);

        EmailDeliveryMessage message = new(
            NotificationRecipient,
            notification.Subject,
            notification.PlainTextBody,
            notification.HtmlBody);

        try
        {
            Result result = await mailGateway.SendAsync(message, context.RequestAborted);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Create Student mail notification failed. ErrorCode={ErrorCode} TraceId={TraceId}",
                    result.Error.Code,
                    context.TraceIdentifier);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Create Student mail notification threw an exception. TraceId={TraceId}",
                context.TraceIdentifier);
        }
    }

    private sealed record CreateStudentNotification(
        string Subject,
        string PlainTextBody,
        string HtmlBody)
    {
        public static CreateStudentNotification FromResponse(
            string appName,
            DateTime sentAtUtc,
            int statusCode,
            string traceId,
            string responseBody)
        {
            ParsedCreateStudentResponse parsed = ParsedCreateStudentResponse.Parse(responseBody, traceId);
            bool succeeded = statusCode is >= 200 and < 300 && parsed.Success is not false;
            string outcome = succeeded ? "succeeded" : "failed";
            string safeAppName = string.IsNullOrWhiteSpace(appName) ? "MOE SEEDS" : appName.Trim();
            string subject = $"{safeAppName} - Create Student {outcome}";

            string timestamp = sentAtUtc.ToString("O", CultureInfo.InvariantCulture);
            string message = parsed.Message ?? "No response message.";
            string errors = parsed.Errors.Count == 0
                ? "None"
                : string.Join("; ", parsed.Errors);

            List<string> lines =
            [
                $"App: {safeAppName}",
                $"Outcome: {outcome}",
                $"Timestamp UTC: {timestamp}",
                $"Status code: {statusCode}",
                $"Trace ID: {parsed.TraceId ?? traceId}",
                $"Message: {message}",
                $"Errors: {errors}"
            ];

            if (parsed.Data.Count > 0)
            {
                lines.Add("Student:");
                foreach ((string key, string value) in parsed.Data)
                {
                    lines.Add($"- {key}: {value}");
                }
            }

            string plainTextBody = string.Join(Environment.NewLine, lines);
            string htmlBody = BuildHtmlBody(safeAppName, outcome, timestamp, statusCode, parsed, traceId, message, errors);

            return new CreateStudentNotification(subject, plainTextBody, htmlBody);
        }

        private static string BuildHtmlBody(
            string appName,
            string outcome,
            string timestamp,
            int statusCode,
            ParsedCreateStudentResponse parsed,
            string fallbackTraceId,
            string message,
            string errors)
        {
            StringBuilder builder = new();
            builder.Append("<!doctype html><html><body style=\"font-family:Arial,sans-serif;color:#1f2937;\">");
            builder.Append("<h2>")
                .Append(WebUtility.HtmlEncode(appName))
                .Append(" - Create Student ")
                .Append(WebUtility.HtmlEncode(outcome))
                .Append("</h2>");
            builder.Append("<table style=\"border-collapse:collapse;\">");
            AppendRow(builder, "Timestamp UTC", timestamp);
            AppendRow(builder, "Status code", statusCode.ToString(CultureInfo.InvariantCulture));
            AppendRow(builder, "Trace ID", parsed.TraceId ?? fallbackTraceId);
            AppendRow(builder, "Message", message);
            AppendRow(builder, "Errors", errors);
            builder.Append("</table>");

            if (parsed.Data.Count > 0)
            {
                builder.Append("<h3>Student</h3><table style=\"border-collapse:collapse;\">");
                foreach ((string key, string value) in parsed.Data)
                {
                    AppendRow(builder, key, value);
                }

                builder.Append("</table>");
            }

            builder.Append("</body></html>");
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, string label, string value)
        {
            builder.Append("<tr><th style=\"text-align:left;padding:4px 12px 4px 0;\">")
                .Append(WebUtility.HtmlEncode(label))
                .Append("</th><td style=\"padding:4px 0;\">")
                .Append(WebUtility.HtmlEncode(value))
                .Append("</td></tr>");
        }
    }

    private sealed record ParsedCreateStudentResponse(
        bool? Success,
        string? Message,
        IReadOnlyCollection<string> Errors,
        string? TraceId,
        IReadOnlyDictionary<string, string> Data)
    {
        private static readonly string[] StudentDataFields =
        [
            "personId",
            "schoolName",
            "studentNumber",
            "displayName"
        ];

        public static ParsedCreateStudentResponse Parse(string responseBody, string fallbackTraceId)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new ParsedCreateStudentResponse(
                    null,
                    "Response body was empty.",
                    [],
                    fallbackTraceId,
                    new Dictionary<string, string>());
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                JsonElement root = document.RootElement;

                bool? success = TryGetBoolean(root, "success");
                string? message = TryGetString(root, "message");
                string? traceId = TryGetString(root, "traceId") ?? fallbackTraceId;
                IReadOnlyCollection<string> errors = ReadErrors(root);
                IReadOnlyDictionary<string, string> data = ReadStudentData(root);

                return new ParsedCreateStudentResponse(success, message, errors, traceId, data);
            }
            catch (JsonException)
            {
                return new ParsedCreateStudentResponse(
                    null,
                    "Response body was not JSON.",
                    [],
                    fallbackTraceId,
                    new Dictionary<string, string>());
            }
        }

        private static bool? TryGetBoolean(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out JsonElement element)
                && element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? element.GetBoolean()
                    : null;

        private static string? TryGetString(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out JsonElement element)
                && element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : null;

        private static IReadOnlyCollection<string> ReadErrors(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out JsonElement errorsElement)
                || errorsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return errorsElement
                .EnumerateArray()
                .Select(ReadJsonValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        private static IReadOnlyDictionary<string, string> ReadStudentData(JsonElement root)
        {
            if (!root.TryGetProperty("data", out JsonElement dataElement)
                || dataElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>();
            }

            Dictionary<string, string> values = [];
            foreach (string field in StudentDataFields)
            {
                if (!dataElement.TryGetProperty(field, out JsonElement fieldElement))
                {
                    continue;
                }

                string value = ReadJsonValue(fieldElement);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[field] = value;
                }
            }

            return values;
        }

        private static string ReadJsonValue(JsonElement element)
            => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty
            };
    }
}

public static class CreateStudentMailNotificationMiddlewareExtensions
{
    public static IApplicationBuilder UseCreateStudentMailNotification(this IApplicationBuilder app)
        => app.UseMiddleware<CreateStudentMailNotificationMiddleware>();
}
