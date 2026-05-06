using AlertInvestigationAgent.Models;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AlertInvestigationAgent.Services
{
    /// <summary>
    /// Talks to Microsoft Teams via Microsoft Graph to read channel messages
    /// (alert mails posted to the channel) and to post replies with the
    /// investigation summary.
    /// </summary>
    public class TeamsGraphService
    {
        private readonly TeamsOptions _options;
        private readonly ILogger<TeamsGraphService> _logger;
        private readonly GraphServiceClient? _graph;

        public TeamsGraphService(IOptions<TeamsOptions> options, ILogger<TeamsGraphService> logger)
        {
            _options = options.Value;
            _logger = logger;

            if (IsConfigured())
            {
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
                {
                    var cred = new ClientSecretCredential(
                        _options.TenantId, _options.ClientId, _options.ClientSecret);
                    _graph = new GraphServiceClient(cred, scopes);
                }
                else
                {
                    // Fallback: device-code interactive (useful for local dev / hackathon demo)
                    var cred = new DeviceCodeCredential(new DeviceCodeCredentialOptions
                    {
                        TenantId = _options.TenantId,
                        ClientId = _options.ClientId,
                        DeviceCodeCallback = (code, _) =>
                        {
                            _logger.LogWarning("Graph device login required: {Message}", code.Message);
                            Console.WriteLine(code.Message);
                            return Task.CompletedTask;
                        }
                    });
                    _graph = new GraphServiceClient(cred, scopes);
                }
            }
        }

        public bool IsConfigured() =>
            !string.IsNullOrWhiteSpace(_options.TenantId) &&
            !string.IsNullOrWhiteSpace(_options.ClientId) &&
            !string.IsNullOrWhiteSpace(_options.TeamId) &&
            !string.IsNullOrWhiteSpace(_options.ChannelId);

        /// <summary>
        /// Fetches recent messages in the configured channel that are newer than <paramref name="sinceUtc"/>.
        /// </summary>
        public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
            DateTime sinceUtc, CancellationToken ct = default)
        {
            if (_graph is null || !IsConfigured())
            {
                _logger.LogWarning("Teams/Graph not configured; skipping message fetch.");
                return Array.Empty<ChatMessage>();
            }

            try
            {
                var page = await _graph.Teams[_options.TeamId]
                    .Channels[_options.ChannelId]
                    .Messages
                    .GetAsync(req =>
                    {
                        req.QueryParameters.Top = 25;
                    }, ct);

                var msgs = page?.Value ?? new List<ChatMessage>();
                return msgs
                    .Where(m => m.CreatedDateTime is { } d && d.UtcDateTime > sinceUtc)
                    .OrderBy(m => m.CreatedDateTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Teams messages.");
                return Array.Empty<ChatMessage>();
            }
        }

        /// <summary>Posts a reply (HTML) under the given parent channel message.</summary>
        public async Task ReplyToMessageAsync(string parentMessageId, string html, CancellationToken ct = default)
        {
            if (_graph is null || !IsConfigured())
            {
                _logger.LogWarning("Teams/Graph not configured; skipping reply.");
                return;
            }

            try
            {
                var reply = new ChatMessage
                {
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = html
                    }
                };

                await _graph.Teams[_options.TeamId]
                    .Channels[_options.ChannelId]
                    .Messages[parentMessageId]
                    .Replies
                    .PostAsync(reply, cancellationToken: ct);

                _logger.LogInformation("Posted investigation reply to message {Id}", parentMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reply to Teams message {Id}", parentMessageId);
            }
        }

        /// <summary>Returns the raw body content (HTML or text) of the message.</summary>
        public static string GetBodyContent(ChatMessage msg) =>
            msg.Body?.Content ?? msg.Subject ?? string.Empty;
    }
}
