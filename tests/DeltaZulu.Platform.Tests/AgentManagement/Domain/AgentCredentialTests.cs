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

    [TestMethod]
    public void VerifySecretHash_MatchingHash_ReturnsTrue()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);

        Assert.IsTrue(credential.VerifySecretHash("hash-1"));
    }

    [TestMethod]
    public void VerifySecretHash_MismatchedHash_ReturnsFalse()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);

        Assert.IsFalse(credential.VerifySecretHash("hash-2"));
    }

    [TestMethod]
    public void VerifySecretHash_DifferentLengthHash_ReturnsFalse()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);

        Assert.IsFalse(credential.VerifySecretHash("hash-1-but-longer"));
    }

    [TestMethod]
    public void VerifySecretHash_NullOrEmptyPresented_ReturnsFalse()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);

        Assert.IsFalse(credential.VerifySecretHash(null!));
        Assert.IsFalse(credential.VerifySecretHash(""));
    }

    [TestMethod]
    public void Revoke_SetsRevokedAtAndMakesCredentialUnusable()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);
        Assert.IsTrue(credential.IsUsable);

        credential.Revoke(Now.AddMinutes(5));

        Assert.AreEqual(Now.AddMinutes(5), credential.RevokedAt);
        Assert.IsFalse(credential.IsUsable);
    }

    [TestMethod]
    public void Revoke_IsIdempotent()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);

        credential.Revoke(Now.AddMinutes(5));
        credential.Revoke(Now.AddMinutes(10));

        Assert.AreEqual(Now.AddMinutes(5), credential.RevokedAt);
    }

    [TestMethod]
    public void Rotate_AfterRevoke_ClearsRevocation()
    {
        var credential = AgentCredential.Issue(AgentId.New(), "hash-1", Now);
        credential.Revoke(Now.AddMinutes(5));

        credential.Rotate("hash-2", Now.AddMinutes(10));

        Assert.IsNull(credential.RevokedAt);
        Assert.IsTrue(credential.IsUsable);
    }
}
