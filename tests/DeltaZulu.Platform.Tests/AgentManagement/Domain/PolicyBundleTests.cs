using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Policy;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Domain;

[TestClass]
public sealed class PolicyBundleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_RejectsEmptyContentHash()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => PolicyBundle.Create(
            PolicyBundleId.New(), TenantId.Default, AgentId.New(), " ",
            "{}", [], [], null, Now));
        Assert.AreEqual("bundle.content_hash_empty", ex.Code);
    }

    [TestMethod]
    public void Create_RejectsEmptyDocument()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => PolicyBundle.Create(
            PolicyBundleId.New(), TenantId.Default, AgentId.New(), "hash",
            "", [], [], null, Now));
        Assert.AreEqual("bundle.document_empty", ex.Code);
    }

    [TestMethod]
    public void Create_PreservesResolutionIdentity()
    {
        var agentId = AgentId.New();
        var assignmentId = PolicyAssignmentId.New();
        var profileVersionId = ProfileVersionId.New();
        var configVersionId = ConfigVersionId.New();

        var bundle = PolicyBundle.Create(
            PolicyBundleId.New(), TenantId.Default, agentId, "hash", "{}",
            [assignmentId], [profileVersionId], configVersionId, Now);

        Assert.AreEqual(agentId, bundle.AgentId);
        Assert.AreEqual("hash", bundle.ContentHash);
        CollectionAssert.AreEqual(new[] { assignmentId }, bundle.ContributingAssignmentIds.ToArray());
        CollectionAssert.AreEqual(new[] { profileVersionId }, bundle.ProfileVersionIds.ToArray());
        Assert.AreEqual(configVersionId, bundle.ConfigVersionId);
    }
}
