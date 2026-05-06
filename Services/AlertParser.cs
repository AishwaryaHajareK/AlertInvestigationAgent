using System.Net;
using System.Text.RegularExpressions;
using AlertInvestigationAgent.Models;

namespace AlertInvestigationAgent.Services
{
    /// <summary>
    /// Extracts structured alert info (name, severity, description, fired time) from
    /// the raw HTML / text body of a Teams channel message that originated from an
    /// Azure Monitor alert email or a similar source.
    /// </summary>
    public class AlertParser
    {
        private static readonly Regex AlertNameRegex =
            new(@"(?:Alert\s*Name|Alert\s*Rule|Rule\s*Name|Subject)\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);

        private static readonly Regex SeverityRegex =
            new(@"Severity\s*[:\-]\s*(Sev\s*\d|Critical|Error|Warning|Informational|Verbose|\d)", RegexOptions.IgnoreCase);

        private static readonly Regex FiredTimeRegex =
            new(@"(?:Fired\s*Time|Fired\s*At|Fire\s*Time|Triggered\s*At)\s*[:\-]\s*([0-9TZ\-:\s\./]+)", RegexOptions.IgnoreCase);

        private static readonly Regex DescriptionRegex =
            new(@"(?:Description|Summary|Condition)\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);

        public AlertPayload Parse(string? rawHtmlOrText)
        {
            var text = HtmlToText(rawHtmlOrText ?? string.Empty);

            var name = MatchOrEmpty(AlertNameRegex, text);
            var severity = NormalizeSeverity(MatchOrEmpty(SeverityRegex, text));
            var description = MatchOrEmpty(DescriptionRegex, text);
            var firedAt = ParseFiredTime(MatchOrEmpty(FiredTimeRegex, text));

            // Fallbacks
            if (string.IsNullOrWhiteSpace(name))
                name = FirstNonEmptyLine(text) ?? "Unknown Alert";

            if (string.IsNullOrWhiteSpace(severity))
                severity = GuessSeverity(text);

            if (string.IsNullOrWhiteSpace(description))
                description = Truncate(text, 500);

            return new AlertPayload
            {
                AlertName = name.Trim(),
                Severity = severity.Trim(),
                Description = description.Trim(),
                FiredAtUtc = firedAt,
                RawContent = text
            };
        }

        private static string MatchOrEmpty(Regex r, string text)
        {
            var m = r.Match(text);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        private static string NormalizeSeverity(string sev)
        {
            if (string.IsNullOrWhiteSpace(sev)) return string.Empty;
            sev = sev.Trim();
            return sev.ToLowerInvariant() switch
            {
                "0" or "sev0" or "sev 0" or "critical" => "Sev0",
                "1" or "sev1" or "sev 1" or "error" => "Sev1",
                "2" or "sev2" or "sev 2" or "warning" => "Sev2",
                "3" or "sev3" or "sev 3" or "informational" => "Sev3",
                "4" or "sev4" or "sev 4" or "verbose" => "Sev4",
                _ => sev
            };
        }

        private static string GuessSeverity(string text)
        {
            var lower = text.ToLowerInvariant();
            if (lower.Contains("critical") || lower.Contains("sev0")) return "Sev0";
            if (lower.Contains("error") || lower.Contains("sev1")) return "Sev1";
            if (lower.Contains("warning") || lower.Contains("sev2")) return "Sev2";
            return "Sev3";
        }

        private static DateTime ParseFiredTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DateTime.UtcNow;
            return DateTime.TryParse(raw, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;
        }

        private static string? FirstNonEmptyLine(string text)
        {
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0);
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);

        /// <summary>Cheap HTML ? text conversion (no external deps).</summary>
        public static string HtmlToText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // line breaks
            var withBreaks = Regex.Replace(html, @"<\s*(br|/p|/div|/tr|/li)\s*/?>", "\n",
                RegexOptions.IgnoreCase);

            // strip tags
            var noTags = Regex.Replace(withBreaks, "<.*?>", " ", RegexOptions.Singleline);

            // decode entities
            var decoded = WebUtility.HtmlDecode(noTags);

            // collapse whitespace per line
            var lines = decoded.Split('\n')
                .Select(l => Regex.Replace(l, @"[ \t]+", " ").Trim())
                .Where(l => l.Length > 0);

            return string.Join("\n", lines);
        }
    }
}
