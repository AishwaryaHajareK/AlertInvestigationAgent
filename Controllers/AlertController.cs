using Microsoft.AspNetCore.Mvc;
using Microsoft.ApplicationInsights;
using System.Text;
using System.Text.Json;
using AlertInvestigationAgent.Services;

namespace AlertInvestigationAgent.Controllers
{
    [ApiController]
    [Route("api/alert")]
    public class AlertController : ControllerBase
    {
        private readonly InvestigationService _investigationService;
        private readonly TelemetryClient _telemetry;

        public AlertController(TelemetryClient telemetry)
        {
            _telemetry = telemetry;
            _investigationService = new InvestigationService();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveAlert([FromBody] AlertPayload alert)
        {
            // ✅ Track alert in App Insights
            _telemetry.TrackEvent("AlertTriggered", new Dictionary<string, string>
            {
                { "AlertName", alert.AlertName },
                { "Severity", alert.Severity }
            });

            var result = await _investigationService.InvestigateAsync(alert);

            var summary = $@"
                🚨 Automated Alert Investigation 🚨
                
                Alert: {result.AlertName}
                Severity: {result.Severity}
                
                Likely Cause:
                {result.LikelyCause}
                
                Impact:
                - {result.FailedRequests} failed requests
                - Error rate ~{result.ErrorRatePercent}%
                
                Frequency:
                - {result.Occurrences24h} times in last 24 hours
                - {result.Occurrences30d} times in last 30 days
                
                Suggested Actions:
                - {string.Join("\n- ", result.SuggestedActions)}
                ";

            await SendToTeams(summary);

            return Ok("Investigation completed and sent to Teams");
        }

        private async Task SendToTeams(string message)
        {
            var webhookUrl = "PASTE_YOUR_TEAMS_WEBHOOK_URL";

            var payload = new { text = message };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            await client.PostAsync(webhookUrl, content);
        }
    }

    public class AlertPayload
    {
        public string AlertName { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
    }
}