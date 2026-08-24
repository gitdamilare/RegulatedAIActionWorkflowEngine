using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Tests.Api;

internal static class ApiTestRequest
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

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
        string? role = "ProcurementManager")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        AddHeader(request, "X-Tenant-Id", tenantId);
        AddHeader(request, "X-User-Id", userId);
        AddHeader(request, "X-User-Role", role);
        return request;
    }

    internal static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        result.ShouldNotBeNull();
        return result;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter<WorkflowAction>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        return options;
    }

    private static void AddHeader(HttpRequestMessage request, string name, string? value)
    {
        if (value is not null)
        {
            request.Headers.Add(name, value);
        }
    }
}
