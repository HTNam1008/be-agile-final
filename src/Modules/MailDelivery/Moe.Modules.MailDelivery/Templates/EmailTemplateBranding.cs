using System.Net;
using System.Text;
using System.Globalization;

namespace Moe.Modules.MailDelivery.Templates;

public static class EmailTemplateBranding
{
    public const string PrimaryColor = "#DC343B";
    public const string PrimarySoftColor = "#fff1f2";
    public const string PrimaryTextColor = "#9f1239";
    public const string PortalHeroBackgroundColor = "#ffffff";
    public const string PortalHeroBackgroundStyle = "background-color:#ffffff;background-image:radial-gradient(circle at 92% 8%, rgba(239, 51, 64, .08), transparent 24%),linear-gradient(100deg,#fff 0%,#fff 100%);";
    public const string CardBorderColor = "#dce3ee";

    public static void AppendShellStart(StringBuilder builder)
    {
        builder.Append("<!doctype html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head>");
        builder.Append("<body bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"margin:0;padding:0;")
            .Append(PortalHeroBackgroundStyle)
            .Append("font-family:Arial,Helvetica,sans-serif;color:#172033;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"")
            .Append(PortalHeroBackgroundColor)
            .Append("\" style=\"")
            .Append(PortalHeroBackgroundStyle)
            .Append("\">");
        builder.Append("<tr><td align=\"center\" style=\"padding:28px 12px;\">");
        builder.Append("<table role=\"presentation\" width=\"640\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:640px;max-width:100%;background-color:#ffffff;border:1px solid ")
            .Append(CardBorderColor)
            .Append(";border-radius:16px;overflow:hidden;box-shadow:0 12px 28px rgba(15, 23, 42, .08);\">");
    }

    public static void AppendHeader(StringBuilder builder, string title)
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(PrimaryColor)
            .Append("\" style=\"background-color:")
            .Append(PrimaryColor)
            .Append(";padding:26px 30px;color:#ffffff;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        builder.Append("<td style=\"font-size:13px;line-height:18px;letter-spacing:1px;text-transform:uppercase;font-weight:bold;color:#ffe4e6;\">MOE SEEDS</td>");
        builder.Append("</tr><tr><td style=\"font-size:28px;line-height:36px;font-weight:bold;color:#ffffff;padding-top:14px;\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</td></tr></table>");
        builder.Append("</td></tr>");
    }

    public static void AppendButton(StringBuilder builder, string url, string label)
    {
        builder.Append("<table role=\"presentation\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr><td bgcolor=\"")
            .Append(PrimaryColor)
            .Append("\" style=\"background-color:")
            .Append(PrimaryColor)
            .Append(";\">");
        builder.Append("<a href=\"")
            .Append(WebUtility.HtmlEncode(url))
            .Append("\" style=\"display:inline-block;padding:13px 20px;font-size:15px;line-height:20px;color:#ffffff;text-decoration:none;font-weight:bold;\">")
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

    public static void AppendFooter(StringBuilder builder, string message)
    {
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">")
            .Append(WebUtility.HtmlEncode(message))
            .Append("</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
    }

    public static string FormatMoney(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"SGD {amount:N2}");
}
