using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DeltaZulu.Platform.Web.AgentManagement.Hosting;

/// <summary>
/// Registers abuse protection for the unauthenticated bootstrap enrollment route.
/// Authenticated agent routes are bounded by per-agent credentials and the normal
/// hosting limits; enrollment is intentionally partitioned by remote address so a
/// noisy client cannot exhaust bootstrap-token validation work for other clients.
/// </summary>
public static class AgentControlPlaneRateLimitingExtensions
{
    public const string EnrollmentPolicyName = "agent-enrollment";

    public static IServiceCollection AddAgentControlPlaneRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(EnrollmentPolicyName, httpContext =>
            {
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }
}
