using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using static RegulatedAIWorkflow.Tests.Api.ApiTestRequest;

namespace RegulatedAIWorkflow.Tests.Api;

/// <summary>Exercises identity-header binding through the protected endpoints.</summary>
public sealed class IdentityHeaderTests
{
    /// <summary>Both protected endpoints require caller identity headers.</summary>
    [Fact]
    public async Task PostAsync_MissingIdentity_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        foreach (var scenario in new[]
                 {
                     (Path: "/workflows/run", Body: WorkflowBody),
                     (Path: "/approvals", Body: ApprovalBody)
                 })
        {
            using var request = Create(
                scenario.Path,
                scenario.Body,
                tenantId: null,
                userId: null,
                role: null);
            using var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
