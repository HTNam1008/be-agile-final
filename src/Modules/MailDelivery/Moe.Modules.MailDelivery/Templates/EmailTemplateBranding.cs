using System.Net;
using System.Text;
using System.Globalization;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;

namespace Moe.Modules.MailDelivery.Templates;

public static class EmailTemplateBranding
{
    public const string PrimaryColor = "#ef3340";
    public const string PrimarySoftColor = "#fff1f2";
    public const string PrimaryTextColor = "#ef3340";
    public const string PortalHeroBackgroundColor = "#eef3f9";
    public const string PortalHeroBackgroundStyle = "background-color:#eef3f9;";
    public const string CardBorderColor = "#dbe3ee";

    public static void AppendShellStart(StringBuilder builder)
    {
        builder.Append("<!doctype html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head>");
        builder.Append("<body bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"margin:0;padding:0;")
            .Append(PortalHeroBackgroundStyle)
            .Append("font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#0f172a;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"")
            .Append(PortalHeroBackgroundStyle)
            .Append("min-height:100vh;\">");
        builder.Append("<tr><td align=\"center\" style=\"padding:32px 12px;\">");
        builder.Append("<table role=\"presentation\" width=\"640\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:640px;max-width:100%;background-color:#ffffff;border:1px solid ")
            .Append(CardBorderColor)
            .Append(";border-top:6px solid ")
            .Append(PrimaryColor)
            .Append(";border-radius:16px;overflow:hidden;box-shadow:0 4px 12px rgba(15, 23, 42, 0.05);\">");
    }

    public static void AppendHeader(StringBuilder builder, string title)
        => AppendHeader(builder, title, MailDeliveryOptions.DefaultAppName);

    public static void AppendHeader(StringBuilder builder, string title, string appName)
    {
        string brandName = string.IsNullOrWhiteSpace(appName)
            ? MailDeliveryOptions.DefaultAppName
            : appName.Trim();

        builder.Append("<tr><td bgcolor=\"#0f1b2f\" style=\"background-color:#0f1b2f;padding:32px 32px 28px 32px;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"font-size:11px;line-height:16px;letter-spacing:1.5px;text-transform:uppercase;font-weight:700;color:#ef3340;\">")
            .Append(WebUtility.HtmlEncode(brandName))
            .Append("</td>");
        builder.Append("</tr><tr><td style=\"font-size:22px;line-height:30px;font-weight:700;color:#ffffff;padding-top:10px;\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</td></tr></table>");
        builder.Append("</td></tr>");
    }

    public static void AppendButton(StringBuilder builder, string url, string label)
    {
        builder.Append("<table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;margin:8px 0 16px 0;\"><tr><td align=\"center\" bgcolor=\"")
            .Append(PrimaryColor)
            .Append("\" style=\"background-color:")
            .Append(PrimaryColor)
            .Append(";border-radius:8px;\">");
        builder.Append("<a href=\"")
            .Append(WebUtility.HtmlEncode(url))
            .Append("\" style=\"display:inline-block;padding:12px 24px;font-size:14px;line-height:20px;color:#ffffff;text-decoration:none;font-weight:700;letter-spacing:0.5px;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</a>");
        builder.Append("</td></tr></table>");
    }

    public static void AppendSummaryRow(
        StringBuilder builder,
        string label,
        string value,
        string backgroundColor = "#f8fafc",
        string valueColor = "#334155")
    {
        string accentColor = valueColor == "#334155" ? "#94a3b8" : valueColor;

        builder.Append("<tr><td bgcolor=\"")
            .Append(backgroundColor)
            .Append("\" style=\"background-color:")
            .Append(backgroundColor)
            .Append(";padding:12px 16px;border-left:4px solid ")
            .Append(accentColor)
            .Append(";border-bottom:8px solid #ffffff;\">");
        builder.Append("<div style=\"font-size:11px;line-height:16px;color:#64748b;text-transform:uppercase;font-weight:700;letter-spacing:1px;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</div>");
        builder.Append("<div style=\"font-size:16px;line-height:24px;color:")
            .Append(valueColor)
            .Append(";font-weight:700;padding-top:4px;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</div></td></tr>");
    }

    public static void AppendFooter(StringBuilder builder, string message)
    {
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:24px 32px;color:#64748b;font-size:12px;line-height:18px;border-top:1px solid #e2e8f0;\">");
        builder.Append("<div style=\"margin-bottom:8px;font-weight:700;color:#0f1b2f;font-size:13px;\">Ministry of Education, Singapore</div>");
        builder.Append("<div>")
            .Append(WebUtility.HtmlEncode(message))
            .Append("</div>");
        builder.Append("<div style=\"margin-top:16px;font-size:11px;color:#94a3b8;line-height:16px;\">This is an automated system email. Please do not reply directly to this message.</div>");
        builder.Append("</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
    }

    public static string FormatMoney(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"SGD {amount:N2}");

    public static string FormatDate(DateOnly date)
        => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string FormatDate(DateTime utcDate)
        => utcDate.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
