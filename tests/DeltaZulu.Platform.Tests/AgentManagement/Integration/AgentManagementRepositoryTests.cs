using DeltaZulu.Platform.Data.Sqlite.AgentManagement;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.AgentManagement.Integration;

[TestClass]
public sealed class AgentManagementRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private string _databasePath = null!;
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"dz-agentmgmt-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_databasePath}";
        await new SqliteAgentManagementBootstrapper(connectionString).EnsureInitializedAsync();

        var services = new ServiceCollection();
        services.AddAgentManagementSqlitePersistence(connectionString);
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _provider.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    [TestMethod]
    public async Task EnrollmentTokens_RoundTrip_AndLookupByHash()
    {
        var repo = Resolve<IEnrollmentTokenRepository>();
        var token = EnrollmentToken.Create(
            EnrollmentTokenId.New(), TenantId.Default, "onboarding", "token-hash",
            Now.AddHours(4), 3, "tester", Now);
        token.RecordUse(Now);
        repo.Add(token);

        var byHash = await repo.GetByTokenHashAsync("token-hash", TestContext.CancellationToken);
        Assert.IsNotNull(byHash);
        Assert.AreEqual(token.Id, byHash.Id);
        Assert.AreEqual(1, byHash.UseCount);

        byHash.Revoke(Now.AddMinutes(1));
        repo.Save(byHash);

        var reloaded = await repo.GetByIdAsync(token.Id, TestContext.CancellationToken);
        Assert.IsNotNull(reloaded);
        Assert.IsNotNull(reloaded.RevokedAt);
        Assert.HasCount(1, await repo.ListByTenantAsync(TenantId.Default, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AgentCredentials_RoundTrip_AndRotate()
    {
        var repo = Resolve<IAgentCredentialRepository>();
        var agentId = AgentId.New();
        repo.Add(AgentCredential.Issue(agentId, "hash-1", Now));

        var bySecret = await repo.GetBySecretHashAsync("hash-1", TestContext.CancellationToken);
        Assert.IsNotNull(bySecret);
        Assert.AreEqual(agentId, bySecret.Id);

        bySecret.Rotate("hash-2", Now.AddMinutes(2));
        repo.Save(bySecret);

        Assert.IsNull(await repo.GetBySecretHashAsync("hash-1", TestContext.CancellationToken));
        var rotated = await repo.GetByAgentIdAsync(agentId, TestContext.CancellationToken);
        Assert.IsNotNull(rotated);
        Assert.AreEqual("hash-2", rotated.SecretHash);
        Assert.IsNotNull(rotated.RotatedAt);
    }

    [TestMethod]
    public async Task PolicyBundles_RoundTrip_AndHashLookup()
    {
        var repo = Resolve<IPolicyBundleRepository>();
        var agentId = AgentId.New();
        var bundle = PolicyBundle.Create(
            PolicyBundleId.New(), TenantId.Default, agentId, "bundle-hash",
            """{"schemaVersion":"1.0"}""",
            [PolicyAssignmentId.New()], [ProfileVersionId.New()], ConfigVersionId.New(), Now);
        repo.Add(bundle);

        var byHash = await repo.GetByAgentAndHashAsync(agentId, "bundle-hash", TestContext.CancellationToken);
        Assert.IsNotNull(byHash);
        Assert.AreEqual(bundle.Id, byHash.Id);
        Assert.HasCount(1, byHash.ContributingAssignmentIds);
        Assert.HasCount(1, byHash.ProfileVersionIds);
        Assert.IsNotNull(byHash.ConfigVersionId);
        Assert.HasCount(1, await repo.ListByAgentAsync(agentId, TestContext.CancellationToken));
        Assert.IsNull(await repo.GetByAgentAndHashAsync(AgentId.New(), "bundle-hash", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task BundleAcks_LatestWins()
    {
        var repo = Resolve<IBundleAckRepository>();
        var agentId = AgentId.New();
        var bundleId = PolicyBundleId.New();
        repo.Add(new BundleAck(Guid.NewGuid(), agentId, bundleId, BundleAckStatus.Received, null, Now));
        repo.Add(new BundleAck(Guid.NewGuid(), agentId, bundleId, BundleAckStatus.Applied, null, Now.AddMinutes(1)));

        var latest = await repo.GetLatestByAgentAsync(agentId, TestContext.CancellationToken);

        Assert.IsNotNull(latest);
        Assert.AreEqual(BundleAckStatus.Applied, latest.Status);
    }

    [TestMethod]
    public async Task GroupMembership_AddListRemove()
    {
        var repo = Resolve<IAgentGroupRepository>();
        var group = AgentGroup.Create(AgentGroupId.New(), TenantId.Default, "prod", Now);
        repo.Add(group);
        var agentId = AgentId.New();

        repo.AddMember(group.Id, agentId, Now);
        repo.AddMember(group.Id, agentId, Now); // idempotent

        CollectionAssert.AreEqual(new[] { agentId },
            (await repo.ListMemberAgentIdsAsync(group.Id, TestContext.CancellationToken)).ToArray());
        CollectionAssert.AreEqual(new[] { group.Id },
            (await repo.ListGroupIdsForAgentAsync(agentId, TestContext.CancellationToken)).ToArray());

        repo.RemoveMember(group.Id, agentId);
        Assert.IsEmpty(await repo.ListMemberAgentIdsAsync(group.Id, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task PolicyAssignmentPins_RoundTrip_AndClear()
    {
        var repo = Resolve<IPolicyAssignmentRepository>();
        var profileId = ResourceProfileId.New();
        var pinnedProfileVersion = ProfileVersionId.New();
        var configPolicyId = ConfigPolicyId.New();
        var pinnedConfigVersion = ConfigVersionId.New();

        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [profileId], configPolicyId, 0, Now);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = pinnedProfileVersion },
            pinnedConfigVersion, Now);
        repo.Add(assignment);

        var reloaded = await repo.GetByIdAsync(assignment.Id, TestContext.CancellationToken);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(pinnedProfileVersion, reloaded.ProfileVersionPins[profileId]);
        Assert.AreEqual(pinnedConfigVersion, reloaded.PinnedConfigVersionId);

        reloaded.SetPins(new Dictionary<ResourceProfileId, ProfileVersionId>(), null, Now.AddMinutes(1));
        repo.Save(reloaded);

        var cleared = await repo.GetByIdAsync(assignment.Id, TestContext.CancellationToken);
        Assert.IsNotNull(cleared);
        Assert.IsEmpty(cleared.ProfileVersionPins);
        Assert.IsNull(cleared.PinnedConfigVersionId);

        var byScope = await repo.ListByScopeAsync(
            TenantId.Default, AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), TestContext.CancellationToken);
        Assert.HasCount(1, byScope);
    }

    [TestMethod]
    public async Task DaemonConfigVersions_GetLatestPublished()
    {
        var repo = Resolve<IDaemonConfigVersionRepository>();
        var configPolicyId = ConfigPolicyId.New();

        var v1 = TestData.PublishedConfigVersion(configPolicyId, 1, Now);
        var v2 = TestData.PublishedConfigVersion(configPolicyId, 2, Now);
        repo.Add(v1);
        repo.Add(v2);

        var latest = await repo.GetLatestPublishedAsync(configPolicyId, TestContext.CancellationToken);

        Assert.IsNotNull(latest);
        Assert.AreEqual(v2.Id, latest.Id);
        Assert.AreEqual(2, latest.SequenceNumber);
    }

    public TestContext TestContext { get; set; }
}
