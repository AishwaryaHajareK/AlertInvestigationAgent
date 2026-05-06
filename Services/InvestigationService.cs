using AlertInvestigationAgent.Models;

namespace AlertInvestigationAgent.Services
{
    public class InvestigationService
    {
        private readonly KqlQueryService _kql;
        private readonly AiSummarizationService _ai;
        private readonly ILogger<InvestigationService> _logger;

        public InvestigationService(
            KqlQueryService kql,
            AiSummarizationService ai,
            ILogger<InvestigationService> logger)
        {
            _kql = kql;
            _ai = ai;
            _logger = logger;
        }

        public async Task<InvestigationResult> InvestigateAsync(AlertPayload alert)
        {
            _logger.LogInformation("Investigating alert: {AlertName} (severity {Severity})",
                alert.AlertName, alert.Severity);

            AlertHistoryStore.RecordAlert(alert.AlertName);

            var failedRequests = await _kql.GetFailedRequestsLastHourAsync();
            var errorRate = await _kql.GetErrorRateLastHourAsync();
            var occ24 = AlertHistoryStore.CountLastHours(alert.AlertName, 24);
            var occ30 = AlertHistoryStore.CountLastDays(alert.AlertName, 30);

            // Ask the LLM to summarize cause + actions based on the gathered signals.
            var analysis = await _ai.AnalyzeAsync(alert, failedRequests, errorRate, occ24, occ30);

            string likelyCause;
            List<string> actions;

            if (analysis is not null)
            {
                likelyCause = analysis.LikelyCause;
                actions = analysis.SuggestedActions;
            }
            else
            {
                // Minimal safety net (only when AI is unavailable / not configured).
                _logger.LogWarning("Falling back to non-AI summary — Azure OpenAI was unavailable.");
                likelyCause =
                    $"AI analysis unavailable. Observed {failedRequests} failed requests and " +
                    $"~{errorRate}% error rate in the last hour for alert '{alert.AlertName}'.";
                actions = new List<string>
                {
                    "Open Application Insights → Failures and inspect top failing operations",
                    "Review recent deployments / configuration changes",
                    "Check downstream dependencies (DB, cache, external APIs) for latency or errors"
                };
            }

            return new InvestigationResult
            {
                AlertName = alert.AlertName,
                Severity = alert.Severity,
                LikelyCause = likelyCause,
                FailedRequests = failedRequests,
                ErrorRatePercent = errorRate,
                Occurrences24h = occ24,
                Occurrences30d = occ30,
                SuggestedActions = actions
            };
        }
    }
}