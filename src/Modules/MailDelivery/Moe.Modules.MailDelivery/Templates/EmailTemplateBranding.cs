using System.Net;
using System.Text;
using System.Globalization;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;

namespace Moe.Modules.MailDelivery.Templates;

public static class EmailTemplateBranding
{
    public const string PrimaryColor = "#C8102E";
    public const string PrimarySoftColor = "#fff5f5";
    public const string PrimaryTextColor = "#C8102E";
    public const string PortalHeroBackgroundColor = "#e8e8e8";
    public const string PortalHeroBackgroundStyle = "background-color:#e8e8e8;";
    public const string CardBorderColor = "#e5e5e5";

    private const string FontFamily = "Helvetica Neue,Arial,sans-serif";
    private const string BodyTextColor = "#444444";
    private const string HeadingTextColor = "#111111";
    private const string MutedTextColor = "#777777";
    private const string SuccessColor = "#1a7a3c";
    private const string WarningColor = "#9a3412";

    public static void AppendShellStart(StringBuilder builder)
    {
        builder.Append("<!doctype html><html lang=\"en\"><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\"></head>");
        builder.Append("<body bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"margin:0;padding:0;")
            .Append(PortalHeroBackgroundStyle)
            .Append("font-family:")
            .Append(FontFamily)
            .Append(";color:")
            .Append(HeadingTextColor)
            .Append(";\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"")
            .Append(PortalHeroBackgroundStyle)
            .Append("min-height:100vh;padding:28px 0;\">");
        builder.Append("<tr><td align=\"center\" style=\"padding:0 12px;\">");
        builder.Append("<table role=\"presentation\" width=\"600\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:600px;max-width:100%;background-color:#ffffff;border-collapse:separate;border-spacing:0;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.10);\">");
    }

    public static void AppendHeader(StringBuilder builder, string title)
        => AppendHeader(builder, title, MailDeliveryOptions.DefaultAppName);

    public static void AppendHeader(StringBuilder builder, string title, string appName)
    {
        string brandName = string.IsNullOrWhiteSpace(appName)
            ? MailDeliveryOptions.DefaultAppName
            : appName.Trim();

        AppendGovernmentAgencyStrip(builder);
        AppendBrandBar(builder, brandName);
        AppendHero(builder, InferCategory(title), title, InferLeadText(title, brandName));
    }

    public static void AppendButton(StringBuilder builder, string url, string label)
    {
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;margin:8px 0 20px 0;\"><tr><td align=\"center\">");
        builder.Append("<table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;\"><tr><td align=\"center\" bgcolor=\"")
            .Append(PrimaryColor)
            .Append("\" style=\"background-color:")
            .Append(PrimaryColor)
            .Append(";border-radius:8px;\">");
        builder.Append("<a href=\"")
            .Append(WebUtility.HtmlEncode(url))
            .Append("\" style=\"display:inline-block;padding:13px 40px;font-size:13px;line-height:20px;color:#ffffff;text-decoration:none;font-weight:700;letter-spacing:0.02em;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append(" &rarr;</a>");
        builder.Append("</td></tr></table></td></tr></table>");
    }

    public static void AppendSummaryRow(
        StringBuilder builder,
        string label,
        string value,
        string backgroundColor = "#f8fafc",
        string valueColor = "#334155")
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(backgroundColor)
            .Append("\" style=\"background-color:")
            .Append(backgroundColor)
            .Append(";padding:10px 0;border-bottom:1px solid ")
            .Append(CardBorderColor)
            .Append(";font-size:12px;color:")
            .Append(MutedTextColor)
            .Append(";font-family:")
            .Append(FontFamily)
            .Append(";width:50%;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</td>");
        builder.Append("<td bgcolor=\"")
            .Append(backgroundColor)
            .Append("\" align=\"right\" style=\"background-color:")
            .Append(backgroundColor)
            .Append(";padding:10px 0;border-bottom:1px solid ")
            .Append(CardBorderColor)
            .Append(";font-size:13px;line-height:20px;color:")
            .Append(valueColor)
            .Append(";font-weight:600;font-family:")
            .Append(FontFamily)
            .Append(";text-align:right;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</td></tr>");
    }

    public static void AppendFooter(StringBuilder builder, string message)
    {
        builder.Append("<tr><td bgcolor=\"#f5f5f5\" style=\"background-color:#f5f5f5;border-top:1px solid #e5e5e5;padding:24px 28px;\">");
        builder.Append("<p style=\"margin:0 0 8px;font-size:11px;color:#777777;line-height:1.65;font-family:")
            .Append(FontFamily)
            .Append(";\">This is an automated message from the Ministry of Education Student Learning &amp; Finance System (SLFS). Please do not reply to this email.</p>");
        builder.Append("<p style=\"margin:0 0 16px;font-size:11px;color:#777777;line-height:1.65;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(message))
            .Append("</p>");
        builder.Append("<div style=\"height:1px;background:#e5e5e5;margin-bottom:16px;\"></div>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"vertical-align:top;\"><p style=\"margin:0;font-size:10px;color:#aaaaaa;line-height:1.6;font-family:")
            .Append(FontFamily)
            .Append(";\">&copy; 2026 Ministry of Education Singapore. All rights reserved.<br>1 North Buona Vista Drive, Singapore 138675</p></td>");
        builder.Append("<td align=\"right\" style=\"vertical-align:top;\"><p style=\"margin:0;font-size:10px;font-family:")
            .Append(FontFamily)
            .Append(";\"><a href=\"#\" style=\"color:#aaaaaa;text-decoration:none;margin-left:12px;\">Privacy Policy</a><a href=\"#\" style=\"color:#aaaaaa;text-decoration:none;margin-left:12px;\">Terms of Use</a></p></td>");
        builder.Append("</tr></table></td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
    }

    public static void AppendHero(StringBuilder builder, string category, string title, string leadText)
    {
        builder.Append("<tr><td bgcolor=\"#f8f8f8\" style=\"background-color:#f8f8f8;padding:32px 28px 28px;border-bottom:3px solid ")
            .Append(PrimaryColor)
            .Append(";\">");
        builder.Append("<p style=\"margin:0 0 8px;font-size:10px;font-weight:700;letter-spacing:0.14em;text-transform:uppercase;color:")
            .Append(PrimaryColor)
            .Append(";font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(category))
            .Append("</p>");
        builder.Append("<h1 style=\"margin:0 0 10px;font-size:24px;font-weight:800;color:")
            .Append(HeadingTextColor)
            .Append(";line-height:1.25;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</h1>");
        builder.Append("<p style=\"margin:0;font-size:13px;color:")
            .Append(BodyTextColor)
            .Append(";line-height:1.65;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(leadText))
            .Append("</p>");
        builder.Append("</td></tr>");
    }

    public static void AppendSectionLabel(StringBuilder builder, string label)
    {
        builder.Append("<p style=\"margin:0 0 14px;font-size:10px;font-weight:700;letter-spacing:0.14em;text-transform:uppercase;color:")
            .Append(PrimaryColor)
            .Append(";font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</p>");
    }

    public static void AppendNotice(StringBuilder builder, string title, string body, string tone = "info")
    {
        string color = tone.Equals("success", StringComparison.OrdinalIgnoreCase)
            ? SuccessColor
            : tone.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? WarningColor
                : PrimaryColor;
        string background = tone.Equals("success", StringComparison.OrdinalIgnoreCase)
            ? "#e6f7ed"
            : tone.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? "#fff7ed"
                : PrimarySoftColor;

        builder.Append("<div style=\"background:")
            .Append(background)
            .Append(";border-left:4px solid ")
            .Append(color)
            .Append(";border-radius:0 8px 8px 0;padding:14px 18px;margin-bottom:20px;\">");
        builder.Append("<p style=\"margin:0 0 4px;font-size:12px;font-weight:700;color:")
            .Append(color)
            .Append(";font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</p>");
        builder.Append("<p style=\"margin:0;font-size:12px;color:")
            .Append(BodyTextColor)
            .Append(";line-height:1.6;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(body))
            .Append("</p></div>");
    }

    public static void AppendDetailTableStart(StringBuilder builder)
    {
        builder.Append("<div style=\"background:#ffffff;border:1px solid ")
            .Append(CardBorderColor)
            .Append(";border-radius:10px;overflow:hidden;padding:22px;margin-bottom:20px;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;\">");
    }

    public static void AppendDetailTableEnd(StringBuilder builder)
        => builder.Append("</table></div>");

    private static void AppendGovernmentAgencyStrip(StringBuilder builder)
    {
        builder.Append("<tr><td bgcolor=\"#f0f0f0\" style=\"background-color:#f0f0f0;padding:7px 28px;border-bottom:1px solid #dddddd;\">");
        builder.Append("<table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"vertical-align:middle;line-height:0;\">");
        builder.Append("<table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"display:inline-table;width:22px;height:15px;border:1px solid rgba(0,0,0,0.15);border-radius:2px;overflow:hidden;vertical-align:middle;margin-right:7px;\">");
        builder.Append("<tr><td bgcolor=\"#EF3340\" style=\"background:#EF3340;height:7px;padding:0;font-size:6px;line-height:7px;color:#EF3340;text-align:center;\">&#9790;&#9733;&#9733;&#9733;&#9733;&#9733;</td></tr>");
        builder.Append("<tr><td bgcolor=\"#ffffff\" style=\"background:#ffffff;height:8px;padding:0;font-size:5px;line-height:8px;color:#EF3340;text-align:center;letter-spacing:-1px;\">&#9790;&thinsp;&#9733;&thinsp;&#9733;&thinsp;&#9733;</td></tr>");
        builder.Append("</table></td>");
        builder.Append("<td style=\"vertical-align:middle;\"><span style=\"color:#333333;font-size:11.5px;font-weight:500;font-family:")
            .Append(FontFamily)
            .Append(";vertical-align:middle;\">A Singapore Government Agency Website</span></td>");
        builder.Append("</tr></table></td></tr>");
    }

    private static void AppendBrandBar(StringBuilder builder, string brandName)
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(PrimaryColor)
            .Append("\" style=\"background-color:")
            .Append(PrimaryColor)
            .Append(";padding:22px 28px;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"vertical-align:middle;\"><table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"vertical-align:middle;padding-right:12px;\"><div style=\"width:42px;height:42px;background:rgba(255,255,255,0.18);border-radius:8px;text-align:center;line-height:42px;font-size:22px;\">&#127891;</div></td>");
        builder.Append("<td style=\"vertical-align:middle;\"><p style=\"margin:0;font-size:11px;font-weight:700;color:rgba(255,255,255,0.75);letter-spacing:0.12em;text-transform:uppercase;font-family:")
            .Append(FontFamily)
            .Append(";\">Ministry of Education</p>");
        builder.Append("<p style=\"margin:3px 0 0;font-size:10px;color:rgba(255,255,255,0.55);letter-spacing:0.06em;text-transform:uppercase;font-family:")
            .Append(FontFamily)
            .Append(";\">")
            .Append(WebUtility.HtmlEncode(brandName))
            .Append("</p></td>");
        builder.Append("</tr></table></td>");
        builder.Append("<td align=\"right\" style=\"vertical-align:middle;\"><p style=\"margin:0;font-size:10px;color:rgba(255,255,255,0.5);font-weight:600;letter-spacing:0.08em;text-transform:uppercase;font-family:")
            .Append(FontFamily)
            .Append(";\">Student Learning &amp; Finance System</p></td>");
        builder.Append("</tr></table></td></tr>");
    }

    private static string InferCategory(string title)
    {
        if (title.Contains("FAS", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Financial Assistance", StringComparison.OrdinalIgnoreCase)
            || title.Contains("voucher", StringComparison.OrdinalIgnoreCase))
            return "Financial Assistance";
        if (title.Contains("payment", StringComparison.OrdinalIgnoreCase)
            || title.Contains("bill", StringComparison.OrdinalIgnoreCase)
            || title.Contains("installment", StringComparison.OrdinalIgnoreCase)
            || title.Contains("receipt", StringComparison.OrdinalIgnoreCase))
            return "Payment";
        if (title.Contains("course", StringComparison.OrdinalIgnoreCase)
            || title.Contains("enrol", StringComparison.OrdinalIgnoreCase)
            || title.Contains("withdrawal", StringComparison.OrdinalIgnoreCase))
            return "Course Enrolment";
        if (title.Contains("Education Account", StringComparison.OrdinalIgnoreCase)
            || title.Contains("account", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Funds credited", StringComparison.OrdinalIgnoreCase))
            return "Education Account";
        return "Notification";
    }

    private static string InferLeadText(string title, string appName)
    {
        if (title.Contains("approved", StringComparison.OrdinalIgnoreCase))
            return "Your request has been reviewed and approved. Details and next steps are provided below.";
        if (title.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return "Action is required. Please review the details below and try again from the portal.";
        if (title.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
            || title.Contains("withdrawal", StringComparison.OrdinalIgnoreCase))
            return "Your request has been processed. Please review the details below for your records.";
        if (title.Contains("payment", StringComparison.OrdinalIgnoreCase)
            || title.Contains("bill", StringComparison.OrdinalIgnoreCase)
            || title.Contains("installment", StringComparison.OrdinalIgnoreCase))
            return "Payment information has been updated. Please review the details below and keep this email for your records.";
        if (title.Contains("course", StringComparison.OrdinalIgnoreCase)
            || title.Contains("enrol", StringComparison.OrdinalIgnoreCase))
            return "Your course enrolment information has been updated. Please review the details below.";
        if (title.Contains("account", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Funds credited", StringComparison.OrdinalIgnoreCase))
            return "Your account information has been updated. Details and next steps are provided below.";
        return $"This notification was sent by {appName}. Please review the details below.";
    }

    public static string FormatMoney(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"SGD {amount:N2}");

    public static string FormatDate(DateOnly date)
        => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string FormatDate(DateTime utcDate)
        => utcDate.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
