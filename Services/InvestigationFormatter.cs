using System.Text;
using System.Web;
using AlertInvestigationAgent.Models;

namespace AlertInvestigationAgent.Services
{
    /// <summary>
    /// Formats an <see cref="InvestigationResult"/> as HTML (for Teams replies)
    /// or plain text (for the manual API endpoint / webhooks).
    /// </summary>
    public static class InvestigationFormatter
    {
        public static string ToHtml(InvestigationResult r)
        {
            var sb = new StringBuilder();
            sb.Append("<h3>?? Automated Alert Investigation</h3>");
            sb.Append("<p><b>Alert:</b> ").Append(Enc(r.AlertName)).Append("<br/>");
            sb.Append("<b>Severity:</b> ").Append(Enc(r.Severity)).Append("</p>");

            sb.Append("<p><b>Likely Cause:</b><br/>").Append(Enc(r.LikelyCause)).Append("</p>");

            sb.Append("<p><b>Impact (last 1h):</b><ul>")
              .Append("<li>").Append(r.FailedRequests).Append(" failed requests</li>")
              .Append("<li>Error rate ~").Append(r.ErrorRatePercent).Append("%</li>")
              .Append("</ul></p>");

            sb.Append("<p><b>Frequency:</b><ul>")
              .Append("<li>").Append(r.Occurrences24h).Append(" times in last 24h</li>")
              .Append("<li>").Append(r.Occurrences30d).Append(" times in last 30d</li>")
              .Append("</ul></p>");

            sb.Append("<p><b>Suggested Actions:</b><ul>");
            foreach (var a in r.SuggestedActions)
                sb.Append("<li>").Append(Enc(a)).Append("</li>");
            sb.Append("</ul></p>");

            return sb.ToString();
        }

        public static string ToPlainText(InvestigationResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("?? Automated Alert Investigation ??").AppendLine();
            sb.Append("Alert: ").AppendLine(r.AlertName);
            sb.Append("Severity: ").AppendLine(r.Severity).AppendLine();
            sb.AppendLine("Likely Cause:").AppendLine(r.LikelyCause).AppendLine();
            sb.AppendLine("Impact (last 1h):");
            sb.Append(" - ").Append(r.FailedRequests).AppendLine(" failed requests");
            sb.Append(" - Error rate ~").Append(r.ErrorRatePercent).AppendLine("%").AppendLine();
            sb.AppendLine("Frequency:");
            sb.Append(" - ").Append(r.Occurrences24h).AppendLine(" times in last 24h");
            sb.Append(" - ").Append(r.Occurrences30d).AppendLine(" times in last 30d").AppendLine();
            sb.AppendLine("Suggested Actions:");
            foreach (var a in r.SuggestedActions)
                sb.Append(" - ").AppendLine(a);
            return sb.ToString();
        }

        private static string Enc(string? s) => HttpUtility.HtmlEncode(s ?? string.Empty);
    }
}
