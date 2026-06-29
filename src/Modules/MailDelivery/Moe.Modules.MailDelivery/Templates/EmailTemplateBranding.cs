using System.Net;
using System.Text;

namespace Moe.Modules.MailDelivery.Templates;

public static class EmailTemplateBranding
{
    public const string PrimaryColor = "#DC343B";
    public const string PrimarySoftColor = "#fff1f2";
    public const string PrimaryTextColor = "#9f1239";

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
}
