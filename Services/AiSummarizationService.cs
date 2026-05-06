using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlertInvestigationAgent.Models;
using Microsoft.Extensions.Options;

namespace AlertInvestigationAgent.Services
{
    /// <summary>
    /// Uses the organization's FAB Studio agent (generic prompt agent, GPT-4.1-nano)
    /// to dynamically summarize an alert + its observed metrics into a likely cause
    /// and a list of suggested actions. No hard-coded heuristics.
    /// </summary>
    public class AiSummarizationService
    {
        private readonly FabAgentOptions _options;
        private readonly ILogger<AiSummarizationService> _logger;
        private readonly IHttpClientFactory _httpFactory;

        public AiSummarizationService(
            IOptions<FabAgentOptions> options,
            IHttpClientFactory httpFactory,
            ILogger<AiSummarizationService> logger)
        {
            _options = options.Value;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public bool IsConfigured() =>
            !string.IsNullOrWhiteSpace(_options.Endpoint) &&
            !string.IsNullOrWhiteSpace(_options.ApiKey) &&
            !string.IsNullOrWhiteSpace(_options.UserId);

        public record AiAnalysis(string LikelyCause, List<string> SuggestedActions);

        /// <summary>
        /// Asks the FAB agent to analyze the alert and return JSON with
        /// likelyCause + suggestedActions. Returns <c>null</c> on misconfig / failure
        /// so the caller can decide a fallback.
        /// </summary>
        public async Task<AiAnalysis?> AnalyzeAsync(
            AlertPayload alert,
            int failedRequests,
            int errorRatePercent,
            int occurrences24h,
            int occurrences30d,
            CancellationToken ct = default)
        {
            if (!IsConfigured())
            {
                _logger.LogDebug("FAB agent not configured; skipping AI analysis.");
                return null;
            }

            var query = BuildPrompt(alert, failedRequests, errorRatePercent, occurrences24h, occurrences30d);

            try
            {
                using var http = _httpFactory.CreateClient(nameof(AiSummarizationService));
                http.Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));

                using var req = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
                req.Headers.TryAddWithoutValidation("x-authentication", $"api-key {_options.ApiKey}");
                req.Headers.TryAddWithoutValidation("x-user-id", _options.UserId);

                req.Content = JsonContent.Create(new { input = new { query } });

                using var resp = await http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("FAB agent call failed: {Status} {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                var text = ExtractAgentText(body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("FAB agent returned empty content. Raw body: {Body}", body);
                    return null;
                }

                return ParseAnalysisJson(text!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAB agent analysis failed.");
                return null;
            }
        }

        private static string BuildPrompt(
            AlertPayload alert, int failedRequests, int errorRatePercent,
            int occurrences24h, int occurrences30d)
        {
            // We bake the system + user prompt into one string because the FAB agent
            // is a generic single-input prompt agent.
            return $$"""
                You are an SRE assistant that triages production alerts.
                Given the alert below and its observed metrics, produce:
                  1) a concise root-cause hypothesis ("likelyCause"), and
                  2) 3-6 concrete, actionable next steps an on-call engineer should take ("suggestedActions").

                Respond ONLY with strict, minified JSON of this exact shape — no markdown, no commentary:
                {"likelyCause":"...","suggestedActions":["...","..."]}

                Alert details:
                - Name: {{alert.AlertName}}
                - Severity: {{alert.Severity}}
                - Fired (UTC): {{alert.FiredAtUtc:o}}
                - Description: {{alert.Description}}

                Observed signals (Application Insights, last 1 hour):
                - Failed HTTP requests: {{failedRequests}}
                - Error rate: {{errorRatePercent}}%

                Frequency of this alert:
                - {{occurrences24h}} occurrences in last 24 hours
                - {{occurrences30d}} occurrences in last 30 days

                Raw alert content (may be truncated):
                {{Truncate(alert.RawContent ?? alert.Description, 1500)}}
                """;
        }

        /// <summary>
        /// FAB agents typically wrap the model output in an envelope. We try a few
        /// common shapes and fall back to using the entire body as text.
        /// </summary>
        private static string? ExtractAgentText(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Common candidates: { output: "..."} | { output: { answer: "..."}} |
                // { result: "..."} | { data: { output: "..."}} | { response: "...}
                foreach (var path in new[]
                         {
                             new[] { "output" },
                             new[] { "output", "content" },
                             new[] { "output", "answer" },
                             new[] { "output", "text" },
                             new[] { "output", "result" },
                             new[] { "output", "response" },
                             new[] { "output", "message" },
                             new[] { "result" },
                             new[] { "response" },
                             new[] { "answer" },
                             new[] { "content" },
                             new[] { "data", "output" },
                             new[] { "data", "content" },
                             new[] { "data", "answer" }
                         })
                {
                    if (TryGetStringAtPath(root, path, out var s) && !string.IsNullOrWhiteSpace(s))
                        return s;
                }

                // Last resort: serialize root back — caller's regex will still find JSON.
                return root.ToString();
            }
            catch
            {
                // Body wasn't JSON — treat as plain text.
                return body;
            }
        }

        private static bool TryGetStringAtPath(JsonElement root, string[] path, out string? value)
        {
            value = null;
            JsonElement current = root;
            foreach (var seg in path)
            {
                if (current.ValueKind != JsonValueKind.Object) return false;
                if (!current.TryGetProperty(seg, out var next)) return false;
                current = next;
            }
            if (current.ValueKind == JsonValueKind.String)
            {
                value = current.GetString();
                return true;
            }
            return false;
        }

        private static AiAnalysis? ParseAnalysisJson(string text)
        {
            // Find the first {...} block — the model may wrap it in prose / code fences.
            var match = Regex.Match(text, @"\{[\s\S]*\}", RegexOptions.None);
            var json = match.Success ? match.Value : text;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var cause = root.TryGetProperty("likelyCause", out var c)
                    ? c.GetString() ?? string.Empty
                    : string.Empty;

                var actions = new List<string>();
                if (root.TryGetProperty("suggestedActions", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) actions.Add(s!);
                    }
                }

                if (string.IsNullOrWhiteSpace(cause) && actions.Count == 0) return null;
                return new AiAnalysis(cause, actions);
            }
            catch
            {
                return null;
            }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max));
    }
}
