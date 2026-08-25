using System.Net.Http.Json;
using System.Text;

namespace RegulatedAIWorkflow.Tests.Api;

internal static class ApiTestRequest
{
    internal const string DefaultIdempotencyKey = "4d473050-9f9a-4dd7-84c3-6e681efcd55d";

    internal const string WorkflowBody = """
        {
          "vendorId": "silverline-payments",
          "question": "May this vendor process payment data?",
          "requestedAction": "markVendorApproved"
        }
        """;

    internal const string ApprovalBody = """
        {
          "vendorId": "silverline-payments",
          "requestedAction": "markVendorApproved",
          "validForHours": 24
        }
        """;

    internal static HttpRequestMessage Create(
        string path,
        string body,
        string? tenantId = "northstar-bank",
        string? userId = "procurement-user",
        string? role = "ProcurementManager",
        string? idempotencyKey = DefaultIdempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        AddHeader(request, "X-Tenant-Id", tenantId);
        AddHeader(request, "X-User-Id", userId);
        AddHeader(request, "X-User-Role", role);
        if (string.Equals(path, "/workflows/run", StringComparison.Ordinal))
        {
            AddHeader(request, "Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    internal static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var result = await response.Content.ReadFromJsonAsync<T>();
        result.ShouldNotBeNull();
        return result;
    }

    private static void AddHeader(HttpRequestMessage request, string name, string? value)
    {
        if (value is not null)
        {
            request.Headers.Add(name, value);
        }
    }
}
