using System.Text.Json;
using System.Text.Json.Serialization;
using RegulatedAIWorkflow.Api.Endpoints;
using RegulatedAIWorkflow.Core.Application;
using RegulatedAIWorkflow.Core.Application.Approval;
using RegulatedAIWorkflow.Core.Application.Workflow;
using RegulatedAIWorkflow.Core.Contracts.Workflow;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Approval;
using RegulatedAIWorkflow.Infrastructure.Audit;
using RegulatedAIWorkflow.Infrastructure.Evidence;
using RegulatedAIWorkflow.Infrastructure.Execution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter<WorkflowAction>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
});

builder.Services.AddSingleton<IEvidenceRepository, InMemoryEvidenceRepository>();
builder.Services.AddSingleton<IRiskEvaluator, DeterministicRiskEvaluator>();
builder.Services.AddSingleton<IApprovalRepository, InMemoryApprovalRepository>();
builder.Services.AddSingleton<IAuditSink, InMemoryAuditSink>();
builder.Services.AddSingleton<IActionExecutor, InMemoryActionExecutor>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<ApprovalGate>();
builder.Services.AddScoped<ApprovalIssuer>();
builder.Services.AddScoped<WorkflowOrchestrator>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.MapPost("/workflows/run", WorkflowEndpoint.RunAsync)
    .WithName("RunWorkflow");

app.MapPost("/approvals", ApprovalEndpoint.RecordAsync)
    .WithName("IssueApproval");

app.Run();

public partial class Program;
