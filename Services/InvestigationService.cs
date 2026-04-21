using AlertInvestigationAgent.Controllers;

namespace AlertInvestigationAgent.Services
{

    public class InvestigationService
    {
        private readonly KqlQueryService _kql;

        public InvestigationService()
        {
            _kql = new KqlQueryService();
        }

        public async Task<InvestigationResult> InvestigateAsync(AlertPayload alert)
        {
            AlertHistoryStore.RecordAlert();

            var failedRequests =
                await _kql.GetFailedRequestsLastHourAsync();

            var errorRate =
                await _kql.GetErrorRateLastHourAsync();

            return new InvestigationResult
            {
                AlertName = alert.AlertName,
                Severity = alert.Severity,

                LikelyCause = "Increase in failed HTTP requests detected via Application Insights",

                FailedRequests = failedRequests,
                ErrorRatePercent = errorRate,

                Occurrences24h = AlertHistoryStore.CountLastHours(24),
                Occurrences30d = AlertHistoryStore.CountLastDays(30),

                SuggestedActions = new[]
                {
                    "Open Application Insights → Failures",
                    "Check authentication / dependency errors",
                    "Review recent deployments"
                }
            };
        }
    }


    public class InvestigationResult
    {
        public string AlertName { get; set; }
        public string Severity { get; set; }

        public string LikelyCause { get; set; }

        public int FailedRequests { get; set; }
        public int ErrorRatePercent { get; set; }

        public int Occurrences24h { get; set; }
        public int Occurrences30d { get; set; }

        public string[] SuggestedActions { get; set; }
    }
}