using AlertInvestigationAgent.Models;
using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlertInvestigationAgent.Services
{
    public class KqlQueryService
    {
        private readonly LogsQueryClient _client;
        private readonly ILogger<KqlQueryService> _logger;
        private readonly string _workspaceId;

        public KqlQueryService(IOptions<AppInsightsOptions> options, ILogger<KqlQueryService> logger)
        {
            _logger = logger;
            _workspaceId = options.Value.WorkspaceId;

            // DefaultAzureCredential works in Visual Studio, Azure CLI, Managed Identity, etc.
            _client = new LogsQueryClient(new DefaultAzureCredential());
        }

        // ✅ Get failed request count (last 1 hour)
        public async Task<int> GetFailedRequestsLastHourAsync()
        {
            const string kql = @"
                AppRequests
                | where TimeGenerated >= ago(1h)
                | where Success == false
                | summarize count()";

            return await SafeQueryScalarAsync(kql, columnIndex: 0);
        }

        // ✅ Get error rate (last 1 hour)
        public async Task<int> GetErrorRateLastHourAsync()
        {
            const string kql = @"
                AppRequests
                | where TimeGenerated >= ago(1h)
                | summarize
                    total = count(),
                    failed = countif(Success == false)
                | extend errorRate = iff(total == 0, 0.0, (todouble(failed) / todouble(total)) * 100)
                | project errorRate";

            return await SafeQueryScalarAsync(kql, columnIndex: 0);
        }

        private async Task<int> SafeQueryScalarAsync(string kql, int columnIndex)
        {
            if (string.IsNullOrWhiteSpace(_workspaceId))
            {
                _logger.LogWarning("AppInsights:WorkspaceId is not configured. Returning 0.");
                return 0;
            }

            try
            {
                var response = await _client.QueryWorkspaceAsync(
                    _workspaceId,
                    kql,
                    new QueryTimeRange(TimeSpan.FromHours(1)));

                if (response.Value.Table.Rows.Count == 0)
                    return 0;

                var value = response.Value.Table.Rows[0][columnIndex];
                return value is null ? 0 : Convert.ToInt32(Convert.ToDouble(value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KQL query failed.");
                return 0;
            }
        }
    }
}