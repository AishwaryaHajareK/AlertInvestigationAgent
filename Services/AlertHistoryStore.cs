using System;
using System.Collections.Generic;
using System.Linq;

namespace AlertInvestigationAgent.Services
{
    public static class AlertHistoryStore
    {
        private static readonly List<DateTime> _alertTimes = new();
        private static readonly object _lock = new();

        // Record when an alert arrives
        public static void RecordAlert()
        {
            lock (_lock)
            {
                _alertTimes.Add(DateTime.UtcNow);
            }
        }

        // Count alerts in last X hours
        public static int CountLastHours(int hours)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours);

            lock (_lock)
            {
                return _alertTimes.Count(t => t >= cutoff);
            }
        }

        // Count alerts in last X days
        public static int CountLastDays(int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);

            lock (_lock)
            {
                return _alertTimes.Count(t => t >= cutoff);
            }
        }
    }
}