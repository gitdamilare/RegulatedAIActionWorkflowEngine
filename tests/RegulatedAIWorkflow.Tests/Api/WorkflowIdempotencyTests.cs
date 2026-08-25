using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RegulatedAIWorkflow.Api.Dtos;
using RegulatedAIWorkflow.Core.Domain.Execution;
using RegulatedAIWorkflow.Core.Ports;
using RegulatedAIWorkflow.Infrastructure.Audit;
using static RegulatedAIWorkflow.Tests.Api.ApiTestRequest;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>Exercises sequential HTTP idempotency through the real Minimal API host.</summary>
public sealed class WorkflowIdempotencyTests
{
    public static TheoryData<string, string[]?> InvalidKeys => new()
    {
        { "Missing", null },
        { "Duplicate", [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()] },
        { "Malformed", ["not-a-guid"] }
    };

    /// <summary>A workflow request requires exactly one GUID idempotency key.</summary>
    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public async Task PostAsync_InvalidIdempotencyKey_ReturnsProblemDetails(
        string scenario,
        string[]? values)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = Create(
            "/workflows/run",
            WorkflowBody,
            idempotencyKey: null);
        if (values is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", values).ShouldBeTrue();
        }

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, scenario);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(
            "application/problem+json",
            scenario);
        var problem = await ReadAsync<ProblemDetails>(response);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest, scenario);
    }

    /// <summary>An unbound body fails binding without claiming the idempotency key.</summary>
    [Fact]
    public async Task PostAsync_UnboundBody_ReturnsBindingFailureAndLeavesKeyUnclaimed()
    {
        const string key = "6f4b0f2e-88a4-4a3e-9a24-2f1c6d7b0e51";
        var executor = new CountingActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();

        using (var unboundRequest = Create("/workflows/run", "null", idempotencyKey: key))
        using (var unboundResponse = await client.SendAsync(unboundRequest))
        {
            unboundResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            unboundResponse.Content.Headers.ContentType?.MediaType.ShouldBe(
                "application/problem+json");
        }

        var approval = await IssueApprovalAsync(client);
        using var validRequest = Create(
            "/workflows/run",
            ApprovedBody(approval.ApprovalId),
            idempotencyKey: key);
        using var validResponse = await client.SendAsync(validRequest);
        var executed = await ReadAsync<WorkflowResponse>(validResponse);

        executed.ActionStatus.ShouldBe("executed");
        executor.CallCount.ShouldBe(1);
    }

    /// <summary>A sequential retry returns the original response without another effect or audit.</summary>
    [Fact]
    public async Task PostAsync_SequentialReplay_ReturnsOriginalResponse()
    {
        const string rawKey = "b6bd8f66-3e08-4332-8df6-14c512055aac";
        var executor = new CountingActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();
        var approval = await IssueApprovalAsync(client);
        var body = ApprovedBody(approval.ApprovalId);

        using var firstRequest = Create(
            "/workflows/run",
            body,
            idempotencyKey: rawKey);
        using var firstResponse = await client.SendAsync(firstRequest);
        var first = await ReadAsync<WorkflowResponse>(firstResponse);
        var auditSink = (InMemoryAuditSink)factory.Services.GetRequiredService<IAuditSink>();
        var auditCount = auditSink.Events.Count;

        using var replayRequest = Create(
            "/workflows/run",
            body,
            idempotencyKey: rawKey);
        using var replayResponse = await client.SendAsync(replayRequest);
        var replay = await ReadAsync<WorkflowResponse>(replayResponse);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.WorkflowId.ShouldBe(first.WorkflowId);
        replay.AuditEventIds.ShouldBe(first.AuditEventIds);
        executor.CallCount.ShouldBe(1);
        auditSink.Events.Count.ShouldBe(auditCount);
        JsonSerializer.Serialize(first).ShouldNotContain(rawKey);
        JsonSerializer.Serialize(auditSink.Events).ShouldNotContain(rawKey);
    }

    /// <summary>A successful key cannot replay data for changed payload or caller identity.</summary>
    [Fact]
    public async Task PostAsync_ReusedKeyWithChangedInput_ReturnsConflict()
    {
        const string key = "1daec41b-3ddf-4ca5-9d75-1c3df6848111";
        var executor = new CountingActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();
        var approval = await IssueApprovalAsync(client);
        var body = ApprovedBody(approval.ApprovalId);

        using (var firstRequest = Create("/workflows/run", body, idempotencyKey: key))
        using (var firstResponse = await client.SendAsync(firstRequest))
        {
            firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var changedBodyRequest = Create(
            "/workflows/run",
            ApprovedBody(approval.ApprovalId, "A changed question."),
            idempotencyKey: key);
        using var changedBodyResponse = await client.SendAsync(changedBodyRequest);
        using var changedCallerRequest = Create(
            "/workflows/run",
            body,
            userId: "different-user",
            idempotencyKey: key);
        using var changedCallerResponse = await client.SendAsync(changedCallerRequest);

        changedBodyResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        changedCallerResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        executor.CallCount.ShouldBe(1);
    }

    /// <summary>Tenant, vendor, and client key remain independent cache scopes.</summary>
    [Fact]
    public async Task PostAsync_DifferentScope_DoesNotReplayOrConflict()
    {
        const string sharedKey = "17c510f6-c1c4-43a4-9e01-ae700ff0d81e";
        var executor = new CountingActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();
        var northstarApproval = await IssueApprovalAsync(client);
        var harborviewApproval = await IssueApprovalAsync(client, "harborview-bank");

        using var northstarRequest = Create(
            "/workflows/run",
            ApprovedBody(northstarApproval.ApprovalId),
            idempotencyKey: sharedKey);
        using var northstarResponse = await client.SendAsync(northstarRequest);
        using var harborviewRequest = Create(
            "/workflows/run",
            ApprovedBody(harborviewApproval.ApprovalId),
            tenantId: "harborview-bank",
            idempotencyKey: sharedKey);
        using var harborviewResponse = await client.SendAsync(harborviewRequest);
        using var differentVendorRequest = Create(
            "/workflows/run",
            """
            {
              "vendorId": "lakeshore-analytics",
              "requestedAction": "markVendorApproved"
            }
            """,
            idempotencyKey: sharedKey);
        using var differentVendorResponse = await client.SendAsync(differentVendorRequest);
        using var differentKeyRequest = Create(
            "/workflows/run",
            ApprovedBody(northstarApproval.ApprovalId),
            idempotencyKey: "851797db-d507-459c-989f-97692080e589");
        using var differentKeyResponse = await client.SendAsync(differentKeyRequest);

        northstarResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        harborviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        differentVendorResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        differentVendorResponse.StatusCode.ShouldNotBe(HttpStatusCode.Conflict);
        differentKeyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        executor.CallCount.ShouldBe(3);
    }

    /// <summary>A blocked decision is not cached and can be retried with an approval.</summary>
    [Fact]
    public async Task PostAsync_BlockedResponse_IsNotCached()
    {
        const string key = "80fd509c-1837-492e-94b6-2998ad588470";
        var executor = new CountingActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();

        using (var blockedRequest = Create("/workflows/run", WorkflowBody, idempotencyKey: key))
        using (var blockedResponse = await client.SendAsync(blockedRequest))
        {
            var blocked = await ReadAsync<WorkflowResponse>(blockedResponse);
            blocked.ActionStatus.ShouldBe("blocked_pending_approval");
        }

        var approval = await IssueApprovalAsync(client);
        using var approvedRequest = Create(
            "/workflows/run",
            ApprovedBody(approval.ApprovalId),
            idempotencyKey: key);
        using var approvedResponse = await client.SendAsync(approvedRequest);
        var approved = await ReadAsync<WorkflowResponse>(approvedResponse);

        approved.ActionStatus.ShouldBe("executed");
        executor.CallCount.ShouldBe(1);
    }

    /// <summary>A definitive no-effect response is not cached and can be retried.</summary>
    [Fact]
    public async Task PostAsync_ServiceUnavailableResponse_IsNotCached()
    {
        const string key = "9efe9fc2-4ef2-4dcc-9381-56e9dd960268";
        var executor = new FailOnceActionExecutor();
        await using var factory = CreateFactory(executor);
        using var client = factory.CreateClient();
        var approval = await IssueApprovalAsync(client);
        var body = ApprovedBody(approval.ApprovalId);

        using var failedRequest = Create("/workflows/run", body, idempotencyKey: key);
        using var failedResponse = await client.SendAsync(failedRequest);
        using var retryRequest = Create("/workflows/run", body, idempotencyKey: key);
        using var retryResponse = await client.SendAsync(retryRequest);
        var retry = await ReadAsync<WorkflowResponse>(retryResponse);

        failedResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        retry.ActionStatus.ShouldBe("executed");
        executor.CallCount.ShouldBe(2);
    }

    private static WebApplicationFactory<Program> CreateFactory(IActionExecutor executor) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IActionExecutor>();
                    services.AddSingleton(executor);
                });
            });

    private static async Task<ApprovalResponse> IssueApprovalAsync(
        HttpClient client,
        string tenantId = "northstar-bank")
    {
        using var request = Create(
            "/approvals",
            ApprovalBody,
            tenantId: tenantId,
            userId: "risk-approver",
            role: "RiskApprover");
        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<ApprovalResponse>(response);
    }

    private static string ApprovedBody(
        string approvalId,
        string? question = null) => $$"""
        {
          "vendorId": "silverline-payments",
          "question": {{JsonSerializer.Serialize(question)}},
          "requestedAction": "markVendorApproved",
          "approvalId": "{{approvalId}}"
        }
        """;

    private sealed class CountingActionExecutor : IActionExecutor
    {
        private int callCount;

        internal int CallCount => callCount;

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(new ActionExecutionResult(Succeeded: true));
        }
    }

    private sealed class FailOnceActionExecutor : IActionExecutor
    {
        private int callCount;

        internal int CallCount => callCount;

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ActionExecutionResult(
                Succeeded: Interlocked.Increment(ref callCount) > 1));
    }
}
