using Microsoft.Extensions.Primitives;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Identity;

/// <summary>
/// Binds identity from transport headers. In production these values come from a validated token; here
/// they are asserted, not authenticated. What matters for the design is that identity never comes from
/// the request body, so a caller cannot name itself alongside the action it wants performed.
/// </summary>
internal static class IdentityHeaderBinder
{
    internal const string TenantHeader = "X-Tenant-Id";
    internal const string UserHeader = "X-User-Id";
    internal const string RoleHeader = "X-User-Role";

    private const int MaximumIdentifierLength = 128;

    /// <summary>Returns true and a principal, or false and the problem response to return.</summary>
    internal static bool TryBind(
        IHeaderDictionary headers,
        out WorkflowPrincipal principal,
        out IResult? problem)
    {
        principal = null!;

        if (!headers.TryGetValue(TenantHeader, out var tenantValues) ||
            !headers.TryGetValue(UserHeader, out var userValues) ||
            !headers.TryGetValue(RoleHeader, out var roleValues))
        {
            problem = Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Asserted identity headers are required.");
            return false;
        }

        if (!TrySingle(tenantValues, out var tenantId) ||
            !TrySingle(userValues, out var userId) ||
            !TrySingle(roleValues, out var roleValue) ||
            !TryMapRole(roleValue, out var role))
        {
            problem = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Asserted identity headers are invalid.");
            return false;
        }

        principal = new WorkflowPrincipal(tenantId, userId, role);
        problem = null;
        return true;
    }

    private static bool TrySingle(StringValues values, out string value)
    {
        value = values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= MaximumIdentifierLength &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
            !value.Any(char.IsControl);
    }

    private static bool TryMapRole(string value, out UserRole role)
    {
        role = UserRole.Unknown;
        return Enum.TryParse(value, ignoreCase: true, out role) &&
            Enum.IsDefined(role) &&
            role is not UserRole.Unknown &&
            string.Equals(value, Enum.GetName(role), StringComparison.OrdinalIgnoreCase);
    }
}
