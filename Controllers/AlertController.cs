using AlertInvestigationAgent.Models;
using AlertInvestigationAgent.Services;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;

namespace AlertInvestigationAgent.Controllers
{
    [ApiController]
    [Route("api/alert")]
    public class AlertController : ControllerBase
    {
        private readonly InvestigationService _investigationService;
        private readonly AlertParser _parser;
        private readonly TeamsGraphService _teams;
        private readonly TelemetryClient _telemetry;
        private readonly ILogger<AlertController> _logger;

        public AlertController(
            InvestigationService investigationService,
            AlertParser parser,
            TeamsGraphService teams,
            TelemetryClient telemetry,
            ILogger<AlertController> logger)
        {
            _investigationService = investigationService;
            _parser = parser;
            _teams = teams;
            _telemetry = telemetry;
            _logger = logger;
        }

        /// <summary>
        /// Manual ingestion endpoint. Accepts a structured AlertPayload, runs the
        /// investigation pipeline, and returns the formatted summary.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceiveAlert([FromBody] AlertPayload alert)
        {
            if (alert is null) return BadRequest("Alert payload is required.");

            _telemetry.TrackEvent("AlertTriggered", new Dictionary<string, string>
            {
                { "AlertName", alert.AlertName ?? "" },
                { "Severity", alert.Severity ?? "" }
            });

            var result = await _investigationService.InvestigateAsync(alert);
            var summary = InvestigationFormatter.ToPlainText(result);

            _logger.LogInformation("Investigation complete for {AlertName}", alert.AlertName);
            return Ok(new { summary, result });
        }

        /// <summary>
        /// Accepts the raw HTML / text body of a Teams alert message, parses it,
        /// runs the investigation, and returns the result. Useful for testing the
        /// pipeline without Graph access.
        /// </summary>
        [HttpPost("ingest-raw")]
        public async Task<IActionResult> IngestRaw([FromBody] RawAlertRequest body)
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Content))
                return BadRequest("Content is required.");

            var alert = _parser.Parse(body.Content);
            var result = await _investigationService.InvestigateAsync(alert);

            return Ok(new
            {
                parsed = alert,
                summary = InvestigationFormatter.ToPlainText(result),
                result
            });
        }

        /// <summary>
        /// Diagnostic endpoint — returns whether Teams/Graph integration is configured.
        /// </summary>
        [HttpGet("teams-status")]
        public IActionResult TeamsStatus() => Ok(new { configured = _teams.IsConfigured() });

        public class RawAlertRequest
        {
            public string Content { get; set; } = string.Empty;
        }
    }
}