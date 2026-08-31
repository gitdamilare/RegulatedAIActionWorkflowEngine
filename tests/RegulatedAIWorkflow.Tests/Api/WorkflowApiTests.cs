using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RegulatedAIWorkflow.Core.Domain.Risk;
using RegulatedAIWorkflow.Core.Ports;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>
/// Proves the shipped app is actually wired: the whole blocked to approved sequence over HTTP, against
/// the real composition root rather than a hand-built orchestrator.
/// </summary>
public sealed class WorkflowApiTests
{
    [Fact]
    public async Task PostRunApproveRun_ReturnsBlockedThenExecutedOverHttp()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var blocked = await client.SendAsync(Request(
            "/workflows/run",
            Harness.Requester,
            "ProcurementManager",
            new { vendorId = Harness.Vendor, question = "Can we approve this vendor?", requestedAction = "markVendorApproved" }));

        blocked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var blockedBody = await Read(blocked);
        blockedBody.GetProperty("actionStatus").GetString().ShouldBe("blocked_pending_approval");
        blockedBody.GetProperty("riskLevel").GetString().ShouldBe("high");
        blockedBody.GetProperty("requiresApproval").GetBoolean().ShouldBeTrue();
        blockedBody.GetProperty("citations").GetArrayLength().ShouldBeGreaterThan(0);
        blockedBody.GetProperty("missingEvidence").GetArrayLength().ShouldBeGreaterThan(0);
        blockedBody.GetProperty("auditEventIds").GetArrayLength().ShouldBe(2);

        var approved = await client.SendAsync(Request(
            "/approvals",
            Harness.Approver,
            "RiskApprover",
            new { vendorId = Harness.Vendor, requestedAction = "markVendorApproved" }));

        approved.StatusCode.ShouldBe(HttpStatusCode.Created);
        var approvalId = (await Read(approved)).GetProperty("approvalId").GetString();
        approvalId.ShouldNotBeNullOrWhiteSpace();

        var executed = await client.SendAsync(Request(
            "/workflows/run",
            Harness.Requester,
            "ProcurementManager",
            new { vendorId = Harness.Vendor, requestedAction = "markVendorApproved", approvalId }));

        executed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var executedBody = await Read(executed);
        executedBody.GetProperty("actionStatus").GetString().ShouldBe("executed");

        // Approval authorized the effect; it did not lower the assessment.
        executedBody.GetProperty("riskLevel").GetString().ShouldBe("high");
        executedBody.GetProperty("missingEvidence").GetArrayLength().ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// A malformed request and a forbidden role are different answers. Collapsing them, as a null return
    /// from the issuer would, tells a well-formed caller they lack permission and a bad request nothing.
    /// </summary>
    [Theory]
    [InlineData(Harness.Approver, "RiskApprover", "", HttpStatusCode.BadRequest)]
    [InlineData(Harness.Requester, "ProcurementManager", Harness.Vendor, HttpStatusCode.Forbidden)]
    public async Task PostApprovals_BadRequestAndForbidden_AreDistinguished(
        string userId,
        string role,
        string vendorId,
        HttpStatusCode expected)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Request(
            "/approvals",
            userId,
            role,
            new { vendorId, requestedAction = "markVendorApproved" }));

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task PostWorkflowsRun_WithoutIdentityHeaders_IsRejectedBeforeAnyEvaluation()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/workflows/run",
            new { vendorId = Harness.Vendor, requestedAction = "markVendorApproved" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Every status Core can return needs a wire mapping. This one is reachable only through a risk
    /// evaluator that disagrees with retrieval, which the shipped evaluator never does, so it was mapped
    /// in the response DTO and missed in the status switch: the guard fired, the audit event was written,
    /// and the caller got an unhandled exception instead of the result. Asserted over HTTP because that is
    /// the layer where the two mappings can disagree.
    /// </summary>
    [Fact]
    public async Task PostWorkflowsRun_AssessmentCitesUnretrievedEvidence_ReportsAServerFaultWithTheRunRetained()
    {
        var disagrees = new StubRiskEvaluator(new RiskEvaluation(
            RiskLevel.High,
            "Do not approve yet.",
            [new RiskReason("TEST_REASON", "A reason.")],
            [],
            ["document-that-was-never-retrieved"],
            RequiresApproval: false));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRiskEvaluator>();
                services.AddSingleton<IRiskEvaluator>(disagrees);
            }));
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Request(
            "/workflows/run",
            Harness.Requester,
            "ProcurementManager",
            new { vendorId = Harness.Vendor, requestedAction = "markVendorApproved" }));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        // The body survives, so the run stays traceable to the audit events already written for it.
        var body = await Read(response);
        body.GetProperty("actionStatus").GetString().ShouldBe("blocked_evidence_unavailable");
        body.GetProperty("riskLevel").GetString().ShouldBe("unknown");
        body.GetProperty("auditEventIds").GetArrayLength().ShouldBe(2);

        // Nothing was established, so nothing is disclosed.
        body.GetProperty("citations").GetArrayLength().ShouldBe(0);
        body.GetProperty("reasons").GetArrayLength().ShouldBe(0);
    }

    private static HttpRequestMessage Request(string path, string userId, string role, object body) =>
        new(HttpMethod.Post, path)
        {
            Headers =
            {
                { "X-Tenant-Id", Harness.TenantA },
                { "X-User-Id", userId },
                { "X-User-Role", role }
            },
            Content = JsonContent.Create(body)
        };

    private static async Task<JsonElement> Read(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
