using AlertInvestigationAgent.Models;
using Microsoft.Extensions.Options;

namespace AlertInvestigationAgent.Services
{
    /// <summary>
    /// Periodically polls the configured Teams channel for new alert messages,
    /// parses + investigates them, and posts the investigation back as a reply.
    /// </summary>
    public class AlertMonitorBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TeamsOptions _teamsOptions;
        private readonly ILogger<AlertMonitorBackgroundService> _logger;
        private readonly HashSet<string> _processedMessageIds = new();
        private DateTime _lastCheckUtc = DateTime.UtcNow.AddMinutes(-10);

        public AlertMonitorBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<TeamsOptions> teamsOptions,
            ILogger<AlertMonitorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _teamsOptions = teamsOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_teamsOptions.EnablePolling)
            {
                _logger.LogInformation("Teams polling is disabled (Teams:EnablePolling = false).");
                return;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(15, _teamsOptions.PollIntervalSeconds));
            _logger.LogInformation("Alert monitor started. Polling every {Interval}s.", interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in alert monitor loop.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (TaskCanceledException) { /* shutting down */ }
            }
        }

        private async Task PollOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var teams = scope.ServiceProvider.GetRequiredService<TeamsGraphService>();
            var parser = scope.ServiceProvider.GetRequiredService<AlertParser>();
            var investigation = scope.ServiceProvider.GetRequiredService<InvestigationService>();

            if (!teams.IsConfigured())
            {
                _logger.LogDebug("Teams not configured yet; skipping poll.");
                return;
            }

            var messages = await teams.GetRecentMessagesAsync(_lastCheckUtc, ct);
            if (messages.Count == 0) return;

            _logger.LogInformation("Found {Count} new channel messages since {Since:o}.",
                messages.Count, _lastCheckUtc);

            foreach (var msg in messages)
            {
                if (msg.Id is null || !_processedMessageIds.Add(msg.Id))
                    continue;

                var raw = TeamsGraphService.GetBodyContent(msg);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (!LooksLikeAlert(raw, msg.Subject))
                {
                    _logger.LogDebug("Message {Id} does not look like an alert; skipping.", msg.Id);
                    continue;
                }

                var alert = parser.Parse(raw);
                if (!string.IsNullOrWhiteSpace(msg.Subject) &&
                    string.Equals(alert.AlertName, "Unknown Alert", StringComparison.OrdinalIgnoreCase))
                {
                    alert.AlertName = msg.Subject!;
                }

                var result = await investigation.InvestigateAsync(alert);
                var html = InvestigationFormatter.ToHtml(result);

                await teams.ReplyToMessageAsync(msg.Id, html, ct);
            }

            _lastCheckUtc = DateTime.UtcNow;
        }

        private static bool LooksLikeAlert(string body, string? subject)
        {
            var hay = ((subject ?? string.Empty) + " " + body).ToLowerInvariant();
            return hay.Contains("alert")
                || hay.Contains("severity")
                || hay.Contains("fired")
                || hay.Contains("azure monitor")
                || hay.Contains("application insights");
        }
    }
}
