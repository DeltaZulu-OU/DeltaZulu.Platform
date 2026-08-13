using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Domain;

[TestClass]
public sealed class PolicyAssignmentPinTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SetPins_ForAssignedProfile_Persists()
    {
        var profileId = ResourceProfileId.New();
        var versionId = ProfileVersionId.New();
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [profileId], null, 0, Now);

        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = versionId },
            null, Now);

        Assert.AreEqual(versionId, assignment.ProfileVersionPins[profileId]);
    }

    [TestMethod]
    public void SetPins_ForUnassignedProfile_Throws()
    {
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [ResourceProfileId.New()], null, 0, Now);

        var ex = Assert.ThrowsExactly<DomainException>(() => assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId>
            {
                [ResourceProfileId.New()] = ProfileVersionId.New(),
            },
            null, Now));
        Assert.AreEqual("assignment.pin_profile_not_assigned", ex.Code);
    }

    [TestMethod]
    public void SetPins_ConfigPinWithoutConfigPolicy_Throws()
    {
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [ResourceProfileId.New()], null, 0, Now);

        var ex = Assert.ThrowsExactly<DomainException>(() => assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId>(),
            ConfigVersionId.New(), Now));
        Assert.AreEqual("assignment.pin_config_without_policy", ex.Code);
    }

    [TestMethod]
    public void SetPins_EmptyPins_ClearsExisting()
    {
        var profileId = ResourceProfileId.New();
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [profileId], ConfigPolicyId.New(), 0, Now);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId> { [profileId] = ProfileVersionId.New() },
            ConfigVersionId.New(), Now);

        assignment.SetPins(new Dictionary<ResourceProfileId, ProfileVersionId>(), null, Now);

        Assert.IsEmpty(assignment.ProfileVersionPins);
        Assert.IsNull(assignment.PinnedConfigVersionId);
    }

    [TestMethod]
    public void UpdateProfiles_DroppingAPinnedProfile_PrunesItsPin()
    {
        var keptProfileId = ResourceProfileId.New();
        var droppedProfileId = ResourceProfileId.New();
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [keptProfileId, droppedProfileId], null, 0, Now);
        assignment.SetPins(
            new Dictionary<ResourceProfileId, ProfileVersionId>
            {
                [keptProfileId] = ProfileVersionId.New(),
                [droppedProfileId] = ProfileVersionId.New(),
            },
            null, Now);

        assignment.UpdateProfiles([keptProfileId], Now);

        Assert.IsTrue(assignment.ProfileVersionPins.ContainsKey(keptProfileId));
        Assert.IsFalse(assignment.ProfileVersionPins.ContainsKey(droppedProfileId));
    }

    [TestMethod]
    public void UpdateConfigPolicy_ClearingThePolicy_ClearsItsPin()
    {
        var profileId = ResourceProfileId.New();
        var assignment = PolicyAssignment.Create(
            PolicyAssignmentId.New(), TenantId.Default, AssignmentScopeType.Tenant,
            TenantId.Default.Value.ToString("D"), [profileId], ConfigPolicyId.New(), 0, Now);
        assignment.SetPins(new Dictionary<ResourceProfileId, ProfileVersionId>(), ConfigVersionId.New(), Now);

        assignment.UpdateConfigPolicy(null, Now);

        Assert.IsNull(assignment.PinnedConfigVersionId);
    }
}
