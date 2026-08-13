using DeltaZulu.Platform.Domain.AgentManagement.Enrollment;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Domain;

[TestClass]
public sealed class EnrollmentTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private static EnrollmentToken NewToken(int maxUses = 2) => EnrollmentToken.Create(
        EnrollmentTokenId.New(), TenantId.Default, "onboarding", "hash",
        Now.AddHours(24), maxUses, "tester", Now);

    [TestMethod]
    public void Create_RejectsEmptyName()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => EnrollmentToken.Create(
            EnrollmentTokenId.New(), TenantId.Default, " ", "hash", Now.AddHours(1), 1, null, Now));
        Assert.AreEqual("enrollmenttoken.name_empty", ex.Code);
    }

    [TestMethod]
    public void Create_RejectsExpiryInPast()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => EnrollmentToken.Create(
            EnrollmentTokenId.New(), TenantId.Default, "t", "hash", Now.AddHours(-1), 1, null, Now));
        Assert.AreEqual("enrollmenttoken.expiry_in_past", ex.Code);
    }

    [TestMethod]
    public void Create_RejectsInvalidMaxUses()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => EnrollmentToken.Create(
            EnrollmentTokenId.New(), TenantId.Default, "t", "hash", Now.AddHours(1), 0, null, Now));
        Assert.AreEqual("enrollmenttoken.max_uses_invalid", ex.Code);
    }

    [TestMethod]
    public void RecordUse_IncrementsUntilExhausted()
    {
        var token = NewToken(maxUses: 2);

        token.RecordUse(Now);
        token.RecordUse(Now);
        Assert.AreEqual(2, token.UseCount);

        var ex = Assert.ThrowsExactly<DomainException>(() => token.RecordUse(Now));
        Assert.AreEqual("enrollmenttoken.exhausted", ex.Code);
    }

    [TestMethod]
    public void EnsureUsable_ThrowsWhenExpired()
    {
        var token = NewToken();
        var ex = Assert.ThrowsExactly<DomainException>(() => token.EnsureUsable(Now.AddHours(25)));
        Assert.AreEqual("enrollmenttoken.expired", ex.Code);
    }

    [TestMethod]
    public void EnsureUsable_ThrowsWhenRevoked()
    {
        var token = NewToken();
        token.Revoke(Now);

        var ex = Assert.ThrowsExactly<DomainException>(() => token.EnsureUsable(Now));
        Assert.AreEqual("enrollmenttoken.revoked", ex.Code);
        Assert.IsFalse(token.IsUsable(Now));
    }
}
