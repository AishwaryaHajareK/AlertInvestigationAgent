using AlertInvestigationAgent.Models;
using AlertInvestigationAgent.Services;
using Microsoft.ApplicationInsights.Extensibility;

var builder = WebApplication.CreateBuilder(args);

// Telemetry
builder.Services.AddApplicationInsightsTelemetry();

// Options
builder.Services.Configure<TeamsOptions>(
    builder.Configuration.GetSection(TeamsOptions.SectionName));
builder.Services.Configure<AppInsightsOptions>(
    builder.Configuration.GetSection(AppInsightsOptions.SectionName));
builder.Services.Configure<FabAgentOptions>(
    builder.Configuration.GetSection(FabAgentOptions.SectionName));

// HttpClient used by AiSummarizationService to call the FAB agent.
builder.Services.AddHttpClient(nameof(AiSummarizationService));

// Core services
builder.Services.AddSingleton<KqlQueryService>();
builder.Services.AddSingleton<AlertParser>();
builder.Services.AddSingleton<TeamsGraphService>();
builder.Services.AddSingleton<AiSummarizationService>();
builder.Services.AddScoped<InvestigationService>();

// Background poller (Teams channel ? investigate ? reply)
builder.Services.AddHostedService<AlertMonitorBackgroundService>();

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();