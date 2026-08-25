using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using RegulatedAIWorkflow.Api.Dtos;
using RegulatedAIWorkflow.Api.Identity;
using RegulatedAIWorkflow.Core.Contracts.Workflow;

namespace RegulatedAIWorkflow.Api.Idempotency;

internal sealed class IdempotencyFilter(IDistributedCache cache) : IEndpointFilter
{
    internal const string HeaderName = "Idempotency-Key";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var identity = IdentityHeaderBinder.Bind(context.HttpContext.Request.Headers);
        if (identity.Failure is not IdentityBindingFailure.None)
        {
            return await next(context);
        }

        if (!TryGetKey(context.HttpContext.Request.Headers, out var idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A single valid Idempotency-Key header is required.");
        }

        if (context.Arguments.OfType<WorkflowRequest>().SingleOrDefault() is not { } request)
        {
            // The body did not bind, so there is no request to key on. Defer to the
            // framework binding-failure response that unfiltered routes already return.
            return await next(context);
        }

        var principal = identity.Principal!;
        var cacheKey = $"workflow-idempotency:{Fingerprint(new CacheScope(
            principal.TenantId,
            request.RequestedAction,
            request.VendorId,
            idempotencyKey))}";
        var requestFingerprint = Fingerprint(new RequestIdentity(
            principal.TenantId,
            principal.UserId,
            principal.Role,
            request));

        var cachedJson = await cache.GetStringAsync(cacheKey, CancellationToken.None);
        if (cachedJson is not null)
        {
            var cached = JsonSerializer.Deserialize<CachedResponse>(cachedJson)
                ?? throw new InvalidOperationException("The cached workflow response is invalid.");
            if (!string.Equals(
                    cached.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The idempotency key was already used for a different request.");
            }

            return Results.Json(cached.Response, statusCode: cached.StatusCode);
        }

        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } statusResult &&
            result is IValueHttpResult
            {
                Value: WorkflowResponse { ActionStatus: "executed" } response
            })
        {
            var cached = new CachedResponse(
                statusResult.StatusCode ?? StatusCodes.Status200OK,
                requestFingerprint,
                response);
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(cached),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                },
                CancellationToken.None);
        }

        return result;
    }

    private static bool TryGetKey(
        IHeaderDictionary headers,
        out Guid idempotencyKey)
    {
        if (headers.TryGetValue(HeaderName, out var values) &&
            values.Count == 1 &&
            Guid.TryParse(values[0], out idempotencyKey))
        {
            return true;
        }

        idempotencyKey = Guid.Empty;
        return false;
    }

    private static string Fingerprint<T>(T value) =>
        Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private sealed record CacheScope(
        string TenantId,
        WorkflowAction RequestedAction,
        string? VendorId,
        Guid IdempotencyKey);

    private sealed record RequestIdentity(
        string TenantId,
        string UserId,
        UserRole Role,
        WorkflowRequest Request);

    private sealed record CachedResponse(
        int StatusCode,
        string RequestFingerprint,
        WorkflowResponse Response);
}
