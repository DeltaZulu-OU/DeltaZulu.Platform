using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;

namespace DeltaZulu.Platform.Web.Api.AgentManagement;

/// <summary>
/// Resolves the bearer agent secret to an <see cref="AgentId"/> and stores it in
/// <c>HttpContext.Items[AgentIdItemKey]</c>. Requests without a valid credential
/// are rejected with 401 before reaching the endpoint handler.
/// </summary>
public sealed class AgentAuthenticationEndpointFilter(ILogger<AgentAuthenticationEndpointFilter> logger) : IEndpointFilter
{
    public const string AgentIdItemKey = "AgentId";
    private const string BearerPrefix = "Bearer ";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Rejected agent API request to {Path} from {RemoteIp}: missing or malformed bearer credential.",
                httpContext.Request.Path, httpContext.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        var authService = httpContext.RequestServices.GetRequiredService<AgentAuthenticationService>();
        var agentId = await authService.ResolveAgentIdAsync(
            authorization[BearerPrefix.Length..].Trim(), httpContext.RequestAborted);
        if (agentId is null)
        {
            logger.LogWarning(
                "Rejected agent API request to {Path} from {RemoteIp}: bearer credential did not resolve to an active agent.",
                httpContext.Request.Path, httpContext.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        httpContext.Items[AgentIdItemKey] = agentId.Value;
        return await next(context);
    }

    public static AgentId GetAuthenticatedAgentId(HttpContext httpContext) =>
        (AgentId)httpContext.Items[AgentIdItemKey]!;
}
