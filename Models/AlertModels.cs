using System;
using System.Collections.Generic;

namespace AlertInvestigationAgent.Models
{
    /// <summary>
    /// Payload accepted by the manual /api/alert endpoint and produced by the Teams parser.
    /// </summary>
    public class AlertPayload
    {
        public string AlertName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>When the alert fired (UTC). Defaults to now.</summary>
        public DateTime FiredAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Original raw text (mail body / Teams message) — useful for debugging.</summary>
        public string? RawContent { get; set; }
    }

    /// <summary>
    /// Result of running the investigation pipeline.
    /// </summary>
    public class InvestigationResult
    {
        public string AlertName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;

        public string LikelyCause { get; set; } = string.Empty;

        public int FailedRequests { get; set; }
        public int ErrorRatePercent { get; set; }

        public int Occurrences24h { get; set; }
        public int Occurrences30d { get; set; }

        public List<string> SuggestedActions { get; set; } = new();
    }
}
