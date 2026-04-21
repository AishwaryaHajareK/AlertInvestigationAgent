using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace AlertInvestigationAgent.Services
{
    public class KqlQueryService
    {
        private readonly LogsQueryClient _client;

        // 🔴 PASTE YOUR WORKSPACE ID HERE
        private const string WorkspaceId = "86cd9436-8283-4f07-9b2b-df87385900e2";

        public KqlQueryService()
        {
            // ✅ Uses Visual Studio authentication
            _client = new LogsQueryClient(new VisualStudioCredential());
        }

        // ✅ Get failed request count (last 1 hour)
        public async Task<int> GetFailedRequestsLastHourAsync()
        {
            var kql = @"
                    AppRequests
                    | where TimeGenerated >= ago(1h)
                    | where Success == false
                    | summarize count()
                    ";

            var response = await _client.QueryWorkspaceAsync(
                WorkspaceId,
                kql,
                new QueryTimeRange(TimeSpan.FromHours(1)));

            if (response.Value.Table.Rows.Count == 0)
                return 0;

            return Convert.ToInt32(response.Value.Table.Rows[0][0]);
        }

        // ✅ Get error rate (last 1 hour)
        public async Task<int> GetErrorRateLastHourAsync()
        {
            var kql = @"
                    AppRequests
                    | where TimeGenerated >= ago(1h)
                    | summarize
                        total = count(),
                        failed = countif(Success == false)
                    | extend errorRate = (todouble(failed) / todouble(total)) * 100
                    ";

            var response = await _client.QueryWorkspaceAsync(
                WorkspaceId,
                kql,
                new QueryTimeRange(TimeSpan.FromHours(1)));

            if (response.Value.Table.Rows.Count == 0)
                return 0;

            return Convert.ToInt32(response.Value.Table.Rows[0][2]);
        }
    }
}