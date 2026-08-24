using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using RegulatedAIWorkflow.Api.Dtos;
using static RegulatedAIWorkflow.Tests.Api.ApiTestRequest;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>Exercises workflow behavior through the real HTTP host.</summary>
public sealed class WorkflowEndpointTests
{
    public static TheoryData<string, string> BindingFailureBodies => new()
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
        }
    };

    /// <summary>Automatic JSON binding failures return safe Problem Details.</summary>
    [Theory]
    [MemberData(nameof(BindingFailureBodies))]
    public async Task PostAsync_JsonBindingFailure_ReturnsProblemDetails(string scenario, string body)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = Create("/workflows/run", body);
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, scenario);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", scenario);
        var problem = await ReadAsync<ProblemDetails>(response);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest, scenario);
    }

    /// <summary>Valid JSON with an invalid identifier retains Core's structured response.</summary>
    [Fact]
    public async Task PostAsync_InvalidVendorIdentifier_ReturnsCoreValidationResult()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        const string body = """
            {
              "vendorId": " vendor ",
              "requestedAction": "markVendorApproved"
            }
            """;

        using var request = Create("/workflows/run", body);
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var result = await ReadAsync<WorkflowResponse>(response);
        result.ActionStatus.ShouldBe("blocked_invalid_request");
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
            approval.RequestedAction.ShouldBe("markVendorApproved");
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
