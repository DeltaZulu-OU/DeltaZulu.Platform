using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaZulu.Platform.Application.AgentManagement;
using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Data.Sqlite.AgentManagement;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Analytics.Observability;
using DeltaZulu.Platform.Web.AgentManagement.Hosting;
using DeltaZulu.Platform.Web.Api.AgentManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeltaZulu.Platform.Tests.AgentManagement.Integration;

/// <summary>
/// Exercises the real /api/agent/v1 pull loop end to end against a self-hosted
/// Kestrel instance with a temp SQLite database and an in-memory observation sink.
/// </summary>
[TestClass]
public sealed class AgentApiEndpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string _databasePath = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private FakeObservationSink _sink = null!;
    private FakeSourceObservationSink _sourceSink = null!;
    private TestClock _clock = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"dz-agentapi-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_databasePath}";
        await new SqliteAgentManagementBootstrapper(connectionString).EnsureInitializedAsync();

        _sink = new FakeObservationSink();
        _sourceSink = new FakeSourceObservationSink();
        _clock = new TestClock();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.Services.AddSingleton<IAgentObservationSink>(_sink);
        builder.Services.AddSingleton<ISourceObservationSink>(_sourceSink);
        builder.Services.Configure<AgentControlPlaneOptions>(_ => { });
        builder.Services.AddAgentManagementSqlitePersistence(connectionString);
        builder.Services.AddAgentManagementApplication();
        builder.Services.AddAgentManagementValidation();

        _app = builder.Build();
        _app.MapAgentApiV1();
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private async Task<string> CreateEnrollmentTokenAsync(int maxUses = 5)
    {
        using var scope = _app.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<EnrollmentTokenService>();
        var issued = await tokenService.CreateAsync(
            TenantId.Default, "test-token", TimeSpan.FromHours(1), maxUses, null);
        return issued.Plaintext;
    }

    private async Task SeedTenantAssignmentAsync()
    {
        using var scope = _app.Services.CreateScope();
        var profileVersions = scope.ServiceProvider
            .GetRequiredService<DeltaZulu.Platform.Domain.AgentManagement.Contracts.IResourceProfileVersionRepository>();
        var assignments = scope.ServiceProvider.GetRequiredService<PolicyAssignmentService>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<DeltaZulu.Platform.Domain.AgentManagement.Contracts.IAgentManagementUnitOfWork>();

        var profileId = ResourceProfileId.New();
        profileVersions.Add(TestData.PublishedProfileVersion(profileId, 1, _clock.Now));
        await unitOfWork.SaveChangesAsync();

        await assignments.CreateAsync(
            TenantId.Default, DeltaZulu.Platform.Domain.AgentManagement.Enums.AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [profileId], null, 0);
    }

    private async Task<(string AgentId, string Secret)> EnrollAsync(
        string token, string hostname = "api-test-host")
    {
        using var response = await _client.PostAsJsonAsync("/api/agent/v1/enroll",
            new { bootstrapToken = token, hostname, platform = "Linux", agentVersion = "1.0" }, Json);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (body.GetProperty("agentId").GetString()!, body.GetProperty("agentSecret").GetString()!);
    }

    private HttpRequestMessage AuthenticatedRequest(HttpMethod method, string path, string secret, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        return request;
    }

    [TestMethod]
    public async Task FullPullLoop_EnrollHeartbeatPullAck_ClearsDrift()
    {
        await SeedTenantAssignmentAsync();
        var token = await CreateEnrollmentTokenAsync();
        var (agentId, secret) = await EnrollAsync(token);

        // First heartbeat: desired bundle assigned, policy changed.
        using var hb1Request = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", secret,
            new { reportedStatus = "Online", bufferPressure = 0.2, queueDepth = 10L, droppedCount = 0L, forwardFailedCount = 0L });
        using var hb1Response = await _client.SendAsync(hb1Request);
        Assert.AreEqual(HttpStatusCode.OK, hb1Response.StatusCode);
        var hb1 = await hb1Response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var desiredBundleId = hb1.GetProperty("desiredBundleId").GetString();
        Assert.IsNotNull(desiredBundleId);
        Assert.IsTrue(hb1.GetProperty("policyChanged").GetBoolean());

        // Pull the bundle document.
        using var bundleRequest = AuthenticatedRequest(HttpMethod.Get, "/api/agent/v1/policy/bundle", secret);
        using var bundleResponse = await _client.SendAsync(bundleRequest);
        Assert.AreEqual(HttpStatusCode.OK, bundleResponse.StatusCode);
        var bundle = await bundleResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual(desiredBundleId, bundle.GetProperty("bundleId").GetString());
        var contentHash = bundle.GetProperty("contentHash").GetString()!;
        Assert.AreEqual(1, bundle.GetProperty("document").GetProperty("profiles").GetArrayLength());

        // Ack as applied.
        using var ackRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/policy/ack", secret,
            new { bundleId = desiredBundleId, status = "Applied" });
        using var ackResponse = await _client.SendAsync(ackRequest);
        Assert.AreEqual(HttpStatusCode.NoContent, ackResponse.StatusCode);

        // Second heartbeat reporting the applied bundle: no policy change, drift cleared.
        using var hb2Request = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", secret,
            new
            {
                appliedBundleId = desiredBundleId,
                appliedBundleHash = contentHash,
                reportedStatus = "Online",
                bufferPressure = 0.2,
                queueDepth = 10L,
                droppedCount = 0L,
                forwardFailedCount = 0L,
            });
        using var hb2Response = await _client.SendAsync(hb2Request);
        var hb2 = await hb2Response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.IsFalse(hb2.GetProperty("policyChanged").GetBoolean());

        // Inventory reflects the acknowledged bundle.
        using (var scope = _app.Services.CreateScope())
        {
            var agentService = scope.ServiceProvider.GetRequiredService<AgentService>();
            var agent = await agentService.GetByIdAsync(new AgentId(Guid.Parse(agentId)));
            Assert.IsNotNull(agent);
            Assert.AreEqual(agent.DesiredBundleId, agent.CurrentBundleId);
        }

        // Lake observations carry the drift proxy columns.
        Assert.HasCount(2, _sink.Snapshots);
        Assert.AreEqual("default", _sink.Snapshots[0].TenantId);
        Assert.AreEqual(contentHash, _sink.Snapshots[1].DesiredProfileVersionId);
        Assert.AreEqual(contentHash, _sink.Snapshots[1].AppliedProfileVersionId);
    }

    [TestMethod]
    public async Task CommandFlow_IssueDeliverResult_CompletesAudited()
    {
        var token = await CreateEnrollmentTokenAsync();
        var (agentId, secret) = await EnrollAsync(token);

        // Operator issues a command.
        string commandId;
        using (var scope = _app.Services.CreateScope())
        {
            var commandService = scope.ServiceProvider.GetRequiredService<AgentCommandService>();
            var command = await commandService.IssueAsync(
                new AgentId(Guid.Parse(agentId)),
                DeltaZulu.Platform.Domain.AgentManagement.Enums.AgentCommandType.CollectDiagnostics,
                "test-operator");
            commandId = command.Id.Value.ToString("D");
        }

        // Heartbeat delivers it.
        using var hbRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", secret,
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L });
        using var hbResponse = await _client.SendAsync(hbRequest);
        var hb = await hbResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var commands = hb.GetProperty("commands");
        Assert.AreEqual(1, commands.GetArrayLength());
        Assert.AreEqual(commandId, commands[0].GetProperty("commandId").GetString());
        Assert.AreEqual("CollectDiagnostics", commands[0].GetProperty("type").GetString());

        // Agent posts the result.
        using var resultRequest = AuthenticatedRequest(HttpMethod.Post,
            $"/api/agent/v1/commands/{commandId}/result", secret,
            new { succeeded = true, resultJson = """{"service":"running"}""" });
        using var resultResponse = await _client.SendAsync(resultRequest);
        Assert.AreEqual(HttpStatusCode.NoContent, resultResponse.StatusCode);

        // Audit record shows the full lifecycle.
        using (var scope = _app.Services.CreateScope())
        {
            var commandService = scope.ServiceProvider.GetRequiredService<AgentCommandService>();
            var history = await commandService.ListByAgentAsync(new AgentId(Guid.Parse(agentId)));
            Assert.HasCount(1, history);
            Assert.AreEqual(
                DeltaZulu.Platform.Domain.AgentManagement.Enums.AgentCommandStatus.Succeeded,
                history[0].Status);
            Assert.IsNotNull(history[0].DeliveredAt);
            Assert.IsNotNull(history[0].CompletedAt);
            Assert.AreEqual("test-operator", history[0].RequestedBy);
        }

        // A second heartbeat delivers nothing new.
        using var hb2Request = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", secret,
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L });
        using var hb2Response = await _client.SendAsync(hb2Request);
        var hb2 = await hb2Response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual(0, hb2.GetProperty("commands").GetArrayLength());
    }

    [TestMethod]
    public async Task CommandResult_ForAnotherAgentsCommand_IsRejected()
    {
        var token = await CreateEnrollmentTokenAsync();
        var (firstAgentId, firstSecret) = await EnrollAsync(token, "cmd-host-a");
        var (_, secondSecret) = await EnrollAsync(token, "cmd-host-b");

        string commandId;
        using (var scope = _app.Services.CreateScope())
        {
            var commandService = scope.ServiceProvider.GetRequiredService<AgentCommandService>();
            var command = await commandService.IssueAsync(
                new AgentId(Guid.Parse(firstAgentId)),
                DeltaZulu.Platform.Domain.AgentManagement.Enums.AgentCommandType.FlushBuffer, null);
            commandId = command.Id.Value.ToString("D");
        }

        // Deliver to host-a so completion would otherwise be valid.
        using var hbRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", firstSecret,
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L });
        using var hbResponse = await _client.SendAsync(hbRequest);
        Assert.AreEqual(HttpStatusCode.OK, hbResponse.StatusCode);

        using var resultRequest = AuthenticatedRequest(HttpMethod.Post,
            $"/api/agent/v1/commands/{commandId}/result", secondSecret,
            new { succeeded = true });
        using var resultResponse = await _client.SendAsync(resultRequest);

        Assert.AreEqual(HttpStatusCode.BadRequest, resultResponse.StatusCode);
        var problem = await resultResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("command.unknown", problem.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Heartbeat_WithSources_WritesSourceObservations()
    {
        var token = await CreateEnrollmentTokenAsync();
        var (agentId, secret) = await EnrollAsync(token);

        using var hbRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", secret,
            new
            {
                bufferPressure = 0.1,
                queueDepth = 0L,
                droppedCount = 0L,
                forwardFailedCount = 0L,
                sources = new[]
                {
                    new
                    {
                        sourceType = "WindowsEventLog",
                        channel = "Security",
                        isEnabled = true,
                        canRead = true,
                        lastReadAt = DateTimeOffset.UtcNow,
                        readErrorCount = 0L,
                        readCount = 120L,
                        keptAfterFilterCount = 100L,
                        discardedCount = 20L,
                        forwardedCount = 100L,
                        forwardFailedCount = 0L,
                    },
                },
            });
        using var hbResponse = await _client.SendAsync(hbRequest);
        Assert.AreEqual(HttpStatusCode.OK, hbResponse.StatusCode);

        Assert.HasCount(1, _sourceSink.Snapshots);
        var snapshot = _sourceSink.Snapshots[0];
        Assert.AreEqual("Security", snapshot.Channel);
        Assert.AreEqual(agentId, snapshot.AgentId);
        Assert.AreEqual("default", snapshot.TenantId);
        Assert.AreEqual(120L, snapshot.ReadCount);
    }

    [TestMethod]
    public async Task Heartbeat_WithoutBearer_Returns401()
    {
        using var response = await _client.PostAsJsonAsync("/api/agent/v1/heartbeat",
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L }, Json);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Heartbeat_WithUnknownSecret_Returns401()
    {
        using var request = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", "dz-as-bogus",
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L });
        using var response = await _client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Enroll_WithExpiredToken_Returns403WithCode()
    {
        var token = await CreateEnrollmentTokenAsync();
        _clock.Advance(TimeSpan.FromHours(2));

        using var response = await _client.PostAsJsonAsync("/api/agent/v1/enroll",
            new { bootstrapToken = token, hostname = "h", platform = "Linux" }, Json);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("enrollmenttoken.expired", problem.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Enroll_WithUnknownToken_Returns403()
    {
        using var response = await _client.PostAsJsonAsync("/api/agent/v1/enroll",
            new { bootstrapToken = "dz-et-bogus", hostname = "h", platform = "Linux" }, Json);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBundle_WithoutAssignments_Returns404()
    {
        var token = await CreateEnrollmentTokenAsync();
        var (_, secret) = await EnrollAsync(token);

        using var request = AuthenticatedRequest(HttpMethod.Get, "/api/agent/v1/policy/bundle", secret);
        using var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("bundle.none", problem.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Ack_ForAnotherAgentsBundle_IsRejected()
    {
        await SeedTenantAssignmentAsync();
        var token = await CreateEnrollmentTokenAsync();
        var (_, firstSecret) = await EnrollAsync(token, "host-a");
        var (_, secondSecret) = await EnrollAsync(token, "host-b");

        // Give host-a a desired bundle.
        using var hbRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/heartbeat", firstSecret,
            new { bufferPressure = 0.1, queueDepth = 0L, droppedCount = 0L, forwardFailedCount = 0L });
        using var hbResponse = await _client.SendAsync(hbRequest);
        var hb = await hbResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var bundleId = hb.GetProperty("desiredBundleId").GetString();
        Assert.IsNotNull(bundleId);

        // host-b must not be able to ack host-a's bundle.
        using var ackRequest = AuthenticatedRequest(HttpMethod.Post, "/api/agent/v1/policy/ack", secondSecret,
            new { bundleId, status = "Applied" });
        using var ackResponse = await _client.SendAsync(ackRequest);

        Assert.AreEqual(HttpStatusCode.BadRequest, ackResponse.StatusCode);
        var problem = await ackResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("bundle.unknown", problem.GetProperty("code").GetString());
    }
}
