using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using static RegulatedAIWorkflow.Tests.Api.ApiTestRequest;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>Exercises approval behavior through the real HTTP host.</summary>
public sealed class ApprovalEndpointTests
{
    /// <summary>A caller without the risk-approver role cannot record approval.</summary>
    [Fact]
    public async Task PostAsync_NonApproverRole_ReturnsForbidden()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = Create(
            "/approvals",
            ApprovalBody,
            role: "ProcurementManager");
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
