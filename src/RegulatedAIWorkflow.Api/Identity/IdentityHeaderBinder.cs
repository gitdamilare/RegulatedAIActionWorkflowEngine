using Microsoft.Extensions.Primitives;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Identity;

internal enum IdentityBindingFailure
{
    None,
    Missing,
    Invalid
}

internal sealed record IdentityBindingResult(
    WorkflowPrincipal? Principal,
    IdentityBindingFailure Failure);

internal static class IdentityHeaderBinder
{
    internal const string TenantHeader = "X-Tenant-Id";
    internal const string UserHeader = "X-User-Id";
    internal const string RoleHeader = "X-User-Role";

    private const int MaximumIdentifierLength = 128;

    internal static IdentityBindingResult Bind(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(TenantHeader, out var tenantValues) ||
            !headers.TryGetValue(UserHeader, out var userValues) ||
            !headers.TryGetValue(RoleHeader, out var roleValues))
        {
            return new IdentityBindingResult(null, IdentityBindingFailure.Missing);
        }

        if (!TryGetSingleValue(tenantValues, out var tenantId) ||
            !TryGetSingleValue(userValues, out var userId) ||
            !TryGetSingleValue(roleValues, out var roleValue) ||
            !IsValidIdentifier(tenantId) ||
            !IsValidIdentifier(userId) ||
            !IsValidIdentifier(roleValue) ||
            !TryMapRole(roleValue, out var role))
        {
            return new IdentityBindingResult(null, IdentityBindingFailure.Invalid);
        }

        return new IdentityBindingResult(
            new WorkflowPrincipal(tenantId!, userId!, role),
            IdentityBindingFailure.None);
    }

    private static bool TryGetSingleValue(StringValues values, out string? value)
    {
        if (values.Count != 1)
        {
            value = null;
            return false;
        }

        value = values[0];
        return true;
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumIdentifierLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    private static bool TryMapRole(string? value, out UserRole role)
    {
        if (Enum.TryParse<UserRole>(value, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed) &&
            parsed is not UserRole.Unknown &&
            string.Equals(
                value,
                Enum.GetName(parsed),
                StringComparison.OrdinalIgnoreCase))
        {
            role = parsed;
            return true;
        }

        role = UserRole.Unknown;
        return false;
    }
}
