using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Domain;

[TestClass]
public sealed class AgentCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Issue_RejectsEmptyHash()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() =>
            AgentCredential.Issue(AgentId.New(), " ", Now));
        Assert.AreEqual("agentcredential.hash_empty", ex.Code);
    }

    [TestMethod]
    public void Rotate_ReplacesHashAndRecordsRotation()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);
        Assert.IsNull(credential.RotatedAt);

        credential.Rotate("hash-2", Now.AddMinutes(5));

        Assert.AreEqual("hash-2", credential.SecretHash);
        Assert.AreEqual(Now.AddMinutes(5), credential.RotatedAt);
    }
}
