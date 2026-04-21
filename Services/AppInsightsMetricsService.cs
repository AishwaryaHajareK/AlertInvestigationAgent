namespace AlertInvestigationAgent.Services
{
    public class AppInsightsMetricsService
    {
        public int GetFailedRequestCountLastHour()
        {
            return Random.Shared.Next(500, 2000);
        }

        public int GetErrorRatePercent()
        {
            return Random.Shared.Next(5, 25);
        }
    }
}