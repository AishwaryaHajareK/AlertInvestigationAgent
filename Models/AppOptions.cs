namespace AlertInvestigationAgent.Models
{
    /// <summary>
    /// Configuration for connecting to the Microsoft Teams channel via Microsoft Graph.
    /// </summary>
    public class TeamsOptions
    {
        public const string SectionName = "Teams";

        /// <summary>Azure AD tenant id.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Azure AD app (client) id with ChannelMessage.Read.All / Send permissions.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>Client secret (for app-only flow). Leave empty to use interactive / device-code.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Team (group) id from the channel URL.</summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>Channel id from the channel URL (the 19:... part).</summary>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>How often the background service polls for new messages.</summary>
        public int PollIntervalSeconds { get; set; } = 60;

        /// <summary>If true, the background polling service is started.</summary>
        public bool EnablePolling { get; set; } = false;
    }

    /// <summary>
    /// Configuration for Application Insights / Log Analytics workspace queries.
    /// </summary>
    public class AppInsightsOptions
    {
        public const string SectionName = "AppInsights";

        /// <summary>Log Analytics / App Insights workspace id used for KQL queries.</summary>
        public string WorkspaceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for the organization's FAB Studio agent (generic prompt agent)
    /// used to AI-summarize alert investigations.
    /// </summary>
    public class FabAgentOptions
    {
        public const string SectionName = "FabAgent";

        /// <summary>Full execute endpoint URL of the FAB agent (Lambda URL).</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>API key issued by FAB Studio (sent as 'x-authentication: api-key &lt;key&gt;').</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>User id used for the 'x-user-id' header.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>HTTP timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 60;
    }
}
