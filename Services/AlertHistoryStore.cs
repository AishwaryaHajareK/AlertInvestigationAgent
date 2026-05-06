using System;
using System.Collections.Generic;
using System.Linq;

namespace AlertInvestigationAgent.Services
{
    public static class AlertHistoryStore
    {
        private static readonly List<(string Name, DateTime TimeUtc)> _alerts = new();
        private static readonly object _lock = new();

        // Record when an alert arrives
        public static void RecordAlert(string alertName)
        {
            lock (_lock)
            {
                _alerts.Add((alertName ?? string.Empty, DateTime.UtcNow));
            }
        }

        // Count alerts in last X hours
        public static int CountLastHours(string alertName, int hours)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours);
            lock (_lock)
            {
                return _alerts.Count(a =>
                    a.TimeUtc >= cutoff &&
                    string.Equals(a.Name, alertName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Count alerts in last X days
        public static int CountLastDays(string alertName, int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            lock (_lock)
            {
                return _alerts.Count(a =>
                    a.TimeUtc >= cutoff &&
                    string.Equals(a.Name, alertName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}