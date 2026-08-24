using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RegulatedAIWorkflow.Api.Dtos;
using static RegulatedAIWorkflow.Tests.Api.ApiTestRequest;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>Exercises workflow behavior through the real HTTP host.</summary>
public sealed class WorkflowEndpointTests
{
    public static TheoryData<string, string> InvalidBodies => new()
    {
        { "Malformed JSON", "{" },
        {
            "Numeric action",
            """
            {
              "vendorId": "silverline-payments",
              "requestedAction": 1
            }
            """
        },
        {
            "Unknown action",
            """
            {
              "vendorId": "silverline-payments",
              "requestedAction": "deleteEverything"
            }
            """
        },
        {
            "Unexpected identity field",
            """
            {
              "vendorId": "silverline-payments",
              "requestedAction": "markVendorApproved",
              "userId": "forged-user"
            }
            """
        },
        {
            "Invalid vendor identifier",
            """
            {
              "vendorId": " vendor ",
              "requestedAction": "markVendorApproved"
            }
            """
        }
    };

    /// <summary>Malformed JSON and invalid structured input return a bad request.</summary>
    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task PostAsync_InvalidBody_ReturnsBadRequest(string scenario, string body)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = Create("/workflows/run", body);
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, scenario);
    }

    /// <summary>An unauthorized caller receives no evidence-derived assessment data.</summary>
    [Fact]
    public async Task PostAsync_UnauthorizedRole_ReturnsNoEvidence()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = Create("/workflows/run", WorkflowBody, role: "Viewer");
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var result = await ReadAsync<WorkflowResponse>(response);
        result.RiskLevel.ShouldBe("unknown");
        result.ActionStatus.ShouldBe("blocked_unauthorized");
        result.Recommendation.ShouldBeEmpty();
        result.Reasons.ShouldBeEmpty();
        result.Citations.ShouldBeEmpty();
        result.MissingEvidence.ShouldBeEmpty();
    }

    /// <summary>A scope-bound approval can authorize a later matching workflow request.</summary>
    [Fact]
    public async Task PostAsync_MatchingScopeApproval_ExecutesWorkflow()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using (var blockedRequest = Create("/workflows/run", WorkflowBody))
        using (var blockedResponse = await client.SendAsync(blockedRequest))
        {
            blockedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var blocked = await ReadAsync<WorkflowResponse>(blockedResponse);
            blocked.ActionStatus.ShouldBe("blocked_pending_approval");
            blocked.RequiresApproval.ShouldBeTrue();
        }

        ApprovalResponse approval;
        using (var approvalRequest = Create(
                   "/approvals",
                   ApprovalBody,
                   userId: "risk-approver",
                   role: "RiskApprover"))
        using (var approvalResponse = await client.SendAsync(approvalRequest))
        {
            approvalResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
            approval = await ReadAsync<ApprovalResponse>(approvalResponse);
            approval.ApproverUserId.ShouldBe("risk-approver");
        }

        var approvedBody = $$"""
            {
              "vendorId": "silverline-payments",
              "question": "Can this vendor now be activated?",
              "requestedAction": "markVendorApproved",
              "approvalId": "{{approval.ApprovalId}}"
            }
            """;
        using var executionRequest = Create("/workflows/run", approvedBody);
        using var executionResponse = await client.SendAsync(executionRequest);

        executionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var executed = await ReadAsync<WorkflowResponse>(executionResponse);
        executed.ActionStatus.ShouldBe("executed");
        executed.RiskLevel.ShouldBe("high");
        executed.RequiresApproval.ShouldBeTrue();
        executed.Recommendation.ShouldBe(
            "Proceeded under recorded approval. The assessment remains high and the evidence gaps listed below are still outstanding.");
    }
}
