using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class PolicyResolutionServiceTests
{
    private readonly TestClock _clock = new();
    private readonly FakeAgentRepository _agents = new();
    private readonly FakeAgentGroupRepository _groups = new();
    private readonly FakePolicyAssignmentRepository _assignments = new();
    private readonly FakeResourceProfileVersionRepository _profileVersions = new();
    private readonly FakeDaemonConfigVersionRepository _configVersions = new();
    private readonly FakePolicyBundleRepository _bundles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private PolicyResolutionService CreateService() => new(
        _assignments, _groups, _profileVersions, _configVersions,
        _bundles, _agents, _unitOfWork, _clock);

    private Agent NewAgent()
    {
        var agent = Agent.Enroll(AgentId.New(), TenantId.Default, "host-1",
            ResourcePlatform.Windows, _clock.Now);
        _agents.Add(agent);
        return agent;
    }

    private PolicyAssignment AddAssignment(
        AssignmentScopeType scopeType, string scopeId,
        IReadOnlyList<ResourceProfileId> profileIds, ConfigPolicyId? configPolicyId = null,
        int precedence = 0)
    {
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, scopeType, scopeId,
            profileIds, configPolicyId, precedence, _clock.Now);
        _assignments.Add(assignment);
        return assignment;
    }

    private ResourceProfileId AddPublishedProfile(int sequence = 1)
    {
        var profileId = ResourceProfileId.New();
        _profileVersions.Add(TestData.PublishedProfileVersion(profileId, sequence, _clock.Now));
        return profileId;
    }

    [TestMethod]
    public async Task Resolve_UnionsProfilesAcrossScopes_WithoutDuplicates()
    {
        var agent = NewAgent();
        var groupId = AgentGroupId.New();
        _groups.Add(AgentGroup.Create(groupId, TenantId.Default, "group-1", _clock.Now));
        _groups.AddMember(groupId, agent.Id, _clock.Now);

        var tenantProfile = AddPublishedProfile();
        var groupProfile = AddPublishedProfile();
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [tenantProfile]);
        AddAssignment(AssignmentScopeType.Group, groupId.Value.ToString("D"), [groupProfile, tenantProfile]);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.AreEqual(2, resolution.ProfileVersionIds.Count);
        Assert.IsEmpty(resolution.UnresolvedProfileIds);
    }

    [TestMethod]
    public async Task Resolve_ConfigPolicy_MostSpecificScopeWins()
    {
        var agent = NewAgent();
        var tenantConfig = ConfigPolicyId.New();
        var agentConfig = ConfigPolicyId.New();
        var tenantVersion = TestData.PublishedConfigVersion(tenantConfig, 1, _clock.Now);
        var agentVersion = TestData.PublishedConfigVersion(agentConfig, 1, _clock.Now);
        _configVersions.Add(tenantVersion);
        _configVersions.Add(agentVersion);

        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"),
            [], tenantConfig);
        AddAssignment(AssignmentScopeType.Agent, agent.Id.Value.ToString("D"),
            [], agentConfig);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.AreEqual(agentVersion.Id, resolution.ConfigVersionId);
    }

    [TestMethod]
    public async Task Resolve_WithinScope_HighestPrecedenceConfigWins()
    {
        var agent = NewAgent();
        var lowConfig = ConfigPolicyId.New();
        var highConfig = ConfigPolicyId.New();
        _configVersions.Add(TestData.PublishedConfigVersion(lowConfig, 1, _clock.Now));
        var highVersion = TestData.PublishedConfigVersion(highConfig, 1, _clock.Now);
        _configVersions.Add(highVersion);

        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"),
            [], lowConfig, precedence: 1);
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"),
            [], highConfig, precedence: 10);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.AreEqual(highVersion.Id, resolution.ConfigVersionId);
    }

    [TestMethod]
    public async Task Resolve_SelectsLatestPublishedProfileVersion()
    {
        var agent = NewAgent();
        var profileId = ResourceProfileId.New();
        _profileVersions.Add(TestData.PublishedProfileVersion(profileId, 1, _clock.Now));
        var latest = TestData.PublishedProfileVersion(profileId, 3, _clock.Now);
        _profileVersions.Add(latest);

        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { latest.Id }, resolution.ProfileVersionIds.ToArray());
    }

    [TestMethod]
    public async Task Resolve_SkipsUnpublishedProfiles_AndReportsThem()
    {
        var agent = NewAgent();
        var unpublishedProfile = ResourceProfileId.New();
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"),
            [unpublishedProfile]);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.IsEmpty(resolution.ProfileVersionIds);
        CollectionAssert.AreEqual(new[] { unpublishedProfile }, resolution.UnresolvedProfileIds.ToArray());
        Assert.IsTrue(resolution.IsEmpty);
    }

    [TestMethod]
    public async Task Resolve_ProducesDeterministicContentHash()
    {
        var agent = NewAgent();
        var profileId = AddPublishedProfile();
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);

        var service = CreateService();
        var first = await service.ResolveAsync(agent, TestContext.CancellationToken);
        var second = await service.ResolveAsync(agent, TestContext.CancellationToken);

        Assert.AreEqual(first.ContentHash, second.ContentHash);
    }

    [TestMethod]
    public async Task EnsureDesiredBundle_ReusesBundleOnIdenticalResolution()
    {
        var agent = NewAgent();
        var profileId = AddPublishedProfile();
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);

        var service = CreateService();
        var first = await service.EnsureDesiredBundleAsync(agent, TestContext.CancellationToken);
        var second = await service.EnsureDesiredBundleAsync(agent, TestContext.CancellationToken);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(first.Id, second.Id);
        Assert.HasCount(1, _bundles.Bundles);
        Assert.AreEqual(first.Id, agent.DesiredBundleId);
    }

    [TestMethod]
    public async Task EnsureDesiredBundle_CreatesNewBundleWhenResolutionChanges()
    {
        var agent = NewAgent();
        var profileId = AddPublishedProfile();
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);

        var service = CreateService();
        var first = await service.EnsureDesiredBundleAsync(agent, TestContext.CancellationToken);

        var secondProfile = AddPublishedProfile();
        AddAssignment(AssignmentScopeType.Agent, agent.Id.Value.ToString("D"), [secondProfile]);
        var second = await service.EnsureDesiredBundleAsync(agent, TestContext.CancellationToken);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(second.Id, agent.DesiredBundleId);
        Assert.HasCount(2, _bundles.Bundles);
    }

    [TestMethod]
    public async Task EnsureDesiredBundle_ReturnsNullWhenNothingApplies()
    {
        var agent = NewAgent();

        var bundle = await CreateService().EnsureDesiredBundleAsync(agent, TestContext.CancellationToken);

        Assert.IsNull(bundle);
        Assert.IsNull(agent.DesiredBundleId);
        Assert.IsEmpty(_bundles.Bundles);
    }

    [TestMethod]
    public async Task Resolve_PinnedProfileVersion_OverridesLatestPublished()
    {
        var agent = NewAgent();
        var profileId = ResourceProfileId.New();
        var v1 = TestData.PublishedProfileVersion(profileId, 1, _clock.Now);
        var v2 = TestData.PublishedProfileVersion(profileId, 2, _clock.Now);
        _profileVersions.Add(v1);
        _profileVersions.Add(v2);

        var assignment = AddAssignment(
            AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = v1.Id },
            null, _clock.Now);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { v1.Id }, resolution.ProfileVersionIds.ToArray());
    }

    [TestMethod]
    public async Task Resolve_MostSpecificPinWins()
    {
        var agent = NewAgent();
        var profileId = ResourceProfileId.New();
        var v1 = TestData.PublishedProfileVersion(profileId, 1, _clock.Now);
        var v2 = TestData.PublishedProfileVersion(profileId, 2, _clock.Now);
        var v3 = TestData.PublishedProfileVersion(profileId, 3, _clock.Now);
        _profileVersions.Add(v1);
        _profileVersions.Add(v2);
        _profileVersions.Add(v3);

        var tenantAssignment = AddAssignment(
            AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);
        tenantAssignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = v1.Id },
            null, _clock.Now);

        var agentAssignment = AddAssignment(
            AssignmentScopeType.Agent, agent.Id.Value.ToString("D"), [profileId]);
        agentAssignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = v2.Id },
            null, _clock.Now);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { v2.Id }, resolution.ProfileVersionIds.ToArray());
    }

    [TestMethod]
    public async Task Resolve_PinToDeprecatedVersion_IsDeliverable()
    {
        var agent = NewAgent();
        var profileId = ResourceProfileId.New();
        var v1 = TestData.PublishedProfileVersion(profileId, 1, _clock.Now);
        v1.Deprecate(_clock.Now);
        var v2 = TestData.PublishedProfileVersion(profileId, 2, _clock.Now);
        _profileVersions.Add(v1);
        _profileVersions.Add(v2);

        var assignment = AddAssignment(
            AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = v1.Id },
            null, _clock.Now);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { v1.Id }, resolution.ProfileVersionIds.ToArray());
    }

    [TestMethod]
    public async Task Resolve_PinToUnknownVersion_BecomesUnresolved()
    {
        var agent = NewAgent();
        var profileId = ResourceProfileId.New();
        _profileVersions.Add(TestData.PublishedProfileVersion(profileId, 1, _clock.Now));

        var assignment = AddAssignment(
            AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [profileId]);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = ProfileVersionId.New() },
            null, _clock.Now);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.IsEmpty(resolution.ProfileVersionIds);
        CollectionAssert.AreEqual(new[] { profileId }, resolution.UnresolvedProfileIds.ToArray());
    }

    [TestMethod]
    public async Task Resolve_PinnedConfigVersion_OverridesLatestPublished()
    {
        var agent = NewAgent();
        var configPolicyId = ConfigPolicyId.New();
        var v1 = TestData.PublishedConfigVersion(configPolicyId, 1, _clock.Now);
        var v2 = TestData.PublishedConfigVersion(configPolicyId, 2, _clock.Now);
        _configVersions.Add(v1);
        _configVersions.Add(v2);

        var assignment = AddAssignment(
            AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"), [], configPolicyId);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId>(), v1.Id, _clock.Now);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);

        Assert.AreEqual(v1.Id, resolution.ConfigVersionId);
    }

    [TestMethod]
    public async Task Resolve_DocumentParsesBackWithProfilesAndConfig()
    {
        var agent = NewAgent();
        var profileId = AddPublishedProfile();
        var configPolicyId = ConfigPolicyId.New();
        _configVersions.Add(TestData.PublishedConfigVersion(configPolicyId, 1, _clock.Now));
        AddAssignment(AssignmentScopeType.Tenant, TenantId.Default.Value.ToString("D"),
            [profileId], configPolicyId);

        var resolution = await CreateService().ResolveAsync(agent, TestContext.CancellationToken);
        var document = PolicyResolutionService.ParseDocument(resolution.DocumentJson);

        Assert.IsNotNull(document);
        Assert.AreEqual(resolution.ContentHash, document.ContentHash);
        Assert.HasCount(1, document.Profiles);
        Assert.IsNotNull(document.Config);
    }

    public TestContext TestContext { get; set; }
}
