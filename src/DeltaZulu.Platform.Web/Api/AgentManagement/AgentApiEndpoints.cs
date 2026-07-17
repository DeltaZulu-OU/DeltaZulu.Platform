using System.Text.Json;
using DeltaZulu.Platform.Application.AgentManagement;
using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;
using DeltaZulu.Platform.Web.AgentManagement.Hosting;
using DeltaZulu.Platform.Web.Api.AgentManagement.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DeltaZulu.Platform.Web.Api.AgentManagement;

/// <summary>
/// Agent-facing pull API. Endpoints stay thin over application services:
/// enroll exchanges a bootstrap token for an identity + secret; heartbeat reports
/// health and returns the desired bundle identity (the pull trigger); the bundle
/// endpoint serves the composed document; ack records the apply outcome.
/// </summary>
public static class AgentApiEndpoints
{
    public static IEndpointRouteBuilder MapAgentApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/agent/v1");

        api.MapPost("/enroll", EnrollAsync)
            .RequireRateLimiting(AgentControlPlaneRateLimitingExtensions.EnrollmentPolicyName);

        var authenticated = api.MapGroup("")
            .AddEndpointFilter<AgentAuthenticationEndpointFilter>();
        authenticated.MapPost("/heartbeat", HeartbeatAsync);
        authenticated.MapGet("/policy/bundle", GetBundleAsync);
        authenticated.MapPost("/policy/ack", AckAsync);
        authenticated.MapPost("/commands/{commandId}/result", CommandResultAsync);

        return app;
    }

    private static async Task<IResult> EnrollAsync(
        EnrollRequest request,
        AgentEnrollmentService enrollmentService,
        IOptions<AgentControlPlaneOptions> options,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BootstrapToken)
            || string.IsNullOrWhiteSpace(request.Hostname))
        {
            return Problem(StatusCodes.Status400BadRequest, "enroll.request_invalid",
                "Bootstrap token and hostname are required.");
        }

        if (!Enum.TryParse<ResourcePlatform>(request.Platform, ignoreCase: true, out var platform))
            return Problem(StatusCodes.Status400BadRequest, "enroll.platform_invalid",
                $"Unknown platform '{request.Platform}'.");

        return await ExecuteAsync(async () =>
        {
            var result = await enrollmentService.EnrollAsync(
                request.BootstrapToken, request.Hostname, platform,
                request.AgentVersion, request.Tags, ct);

            return Results.Ok(new EnrollResponse(
                result.Agent.Id.Value.ToString("D"),
                result.Agent.TenantId.Value.ToString("D"),
                result.AgentSecret,
                options.Value.HeartbeatIntervalSeconds));
        });
    }

    private static async Task<IResult> HeartbeatAsync(
        HeartbeatRequest request,
        HttpContext httpContext,
        AgentCheckInService checkInService,
        CancellationToken ct)
    {
        var agentId = AgentAuthenticationEndpointFilter.GetAuthenticatedAgentId(httpContext);

        PolicyBundleId? appliedBundleId = null;
        if (!string.IsNullOrWhiteSpace(request.AppliedBundleId))
        {
            if (!Guid.TryParse(request.AppliedBundleId, out var appliedGuid))
                return Problem(StatusCodes.Status400BadRequest, "heartbeat.bundle_id_invalid",
                    "Applied bundle id is not a valid identifier.");
            appliedBundleId = new PolicyBundleId(appliedGuid);
        }

        return await ExecuteAsync(async () =>
        {
            var sources = request.Sources?
                .Select(s => new SourceHealthReport(
                    s.SourceType, s.Channel, s.IsEnabled, s.CanRead, s.LastReadAt,
                    s.ReadErrorCount, s.LastError, s.ReadCount, s.KeptAfterFilterCount,
                    s.DiscardedCount, s.ForwardedCount, s.ForwardFailedCount,
                    s.SourceInstanceId, s.ResourceFamily, s.Provider,
                    s.ProfileId, s.ProfileVersionId))
                .ToList();

            var result = await checkInService.HandleHeartbeatAsync(agentId, new HeartbeatReport(
                request.AgentVersion,
                appliedBundleId,
                request.AppliedBundleHash,
                request.ReportedStatus,
                request.BufferPressure,
                request.QueueDepth,
                request.DroppedCount,
                request.ForwardFailedCount,
                sources), ct);

            return Results.Ok(new HeartbeatResponse(
                result.DesiredBundleId?.Value.ToString("D"),
                result.DesiredBundleHash,
                result.PolicyChanged,
                result.Commands
                    .Select(c => new CommandEntry(
                        c.Id.Value.ToString("D"), c.Type.ToString(), c.TimeoutSeconds, c.RequestedAt))
                    .ToList()));
        });
    }

    private static async Task<IResult> CommandResultAsync(
        string commandId,
        CommandResultRequest request,
        HttpContext httpContext,
        AgentCheckInService checkInService,
        CancellationToken ct)
    {
        var agentId = AgentAuthenticationEndpointFilter.GetAuthenticatedAgentId(httpContext);

        if (!Guid.TryParse(commandId, out var commandGuid))
            return Problem(StatusCodes.Status400BadRequest, "command.id_invalid",
                "Command id is not a valid identifier.");

        return await ExecuteAsync(async () =>
        {
            await checkInService.HandleCommandResultAsync(
                agentId, new AgentCommandId(commandGuid), request.Succeeded,
                request.ResultJson, request.Error, ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> GetBundleAsync(
        HttpContext httpContext,
        AgentCheckInService checkInService,
        CancellationToken ct)
    {
        var agentId = AgentAuthenticationEndpointFilter.GetAuthenticatedAgentId(httpContext);

        return await ExecuteAsync(async () =>
        {
            var bundle = await checkInService.GetBundleForAgentAsync(agentId, ct);
            using var document = JsonDocument.Parse(bundle.DocumentJson);

            return Results.Ok(new BundleResponse(
                bundle.Id.Value.ToString("D"),
                bundle.ContentHash,
                bundle.CreatedAt,
                document.RootElement.Clone()));
        });
    }

    private static async Task<IResult> AckAsync(
        AckRequest request,
        HttpContext httpContext,
        AgentCheckInService checkInService,
        CancellationToken ct)
    {
        var agentId = AgentAuthenticationEndpointFilter.GetAuthenticatedAgentId(httpContext);

        if (!Guid.TryParse(request.BundleId, out var bundleGuid))
            return Problem(StatusCodes.Status400BadRequest, "ack.bundle_id_invalid",
                "Bundle id is not a valid identifier.");

        if (!Enum.TryParse<BundleAckStatus>(request.Status, ignoreCase: true, out var status))
            return Problem(StatusCodes.Status400BadRequest, "ack.status_invalid",
                $"Unknown ack status '{request.Status}'.");

        return await ExecuteAsync(async () =>
        {
            await checkInService.HandleAckAsync(
                agentId, new PolicyBundleId(bundleGuid), status, request.Error, ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex)
        {
            return MapDomainException(ex);
        }
    }

    private static IResult MapDomainException(DomainException ex)
    {
        var statusCode = ex.Code switch
        {
            "agent.not_found" or "bundle.none" => StatusCodes.Status404NotFound,
            _ when ex.Code.StartsWith("enrollmenttoken.", StringComparison.Ordinal)
                => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        return Problem(statusCode, ex.Code, ex.Message);
    }

    private static IResult Problem(int statusCode, string code, string message) =>
        Results.Problem(statusCode: statusCode, title: message,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
