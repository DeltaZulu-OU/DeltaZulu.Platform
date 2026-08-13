using DeltaZulu.Platform.Domain.AgentManagement.Commands;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Domain;

[TestClass]
public sealed class AgentCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private static AgentCommand NewCommand(int timeoutSeconds = 300) => AgentCommand.Request(
        AgentCommandId.New(), TenantId.Default, AgentId.New(),
        AgentCommandType.FlushBuffer, "tester", timeoutSeconds, Now);

    [TestMethod]
    public void Request_RejectsUndefinedCommandType()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => AgentCommand.Request(
            AgentCommandId.New(), TenantId.Default, AgentId.New(),
            (AgentCommandType)999, null, 60, Now));
        Assert.AreEqual("command.type_not_allowed", ex.Code);
    }

    [TestMethod]
    public void Request_RejectsInvalidTimeout()
    {
        var ex = Assert.ThrowsExactly<DomainException>(() => AgentCommand.Request(
            AgentCommandId.New(), TenantId.Default, AgentId.New(),
            AgentCommandType.TestOutput, null, 0, Now));
        Assert.AreEqual("command.timeout_invalid", ex.Code);
    }

    [TestMethod]
    public void Lifecycle_DeliverThenCompleteSucceeded()
    {
        var command = NewCommand();

        command.MarkDelivered(Now.AddSeconds(10));
        Assert.AreEqual(AgentCommandStatus.Delivered, command.Status);
        Assert.IsNotNull(command.DeliveredAt);

        command.Complete(true, """{"ok":true}""", null, Now.AddSeconds(20));
        Assert.AreEqual(AgentCommandStatus.Succeeded, command.Status);
        Assert.IsNotNull(command.CompletedAt);
        Assert.IsFalse(command.IsInFlight);
    }

    [TestMethod]
    public void Lifecycle_CompleteWithFailure_RecordsError()
    {
        var command = NewCommand();
        command.MarkDelivered(Now);

        command.Complete(false, null, "service refused restart", Now.AddSeconds(5));

        Assert.AreEqual(AgentCommandStatus.Failed, command.Status);
        Assert.AreEqual("service refused restart", command.Error);
    }

    [TestMethod]
    public void Complete_WithoutDelivery_Throws()
    {
        var command = NewCommand();
        var ex = Assert.ThrowsExactly<DomainException>(() =>
            command.Complete(true, null, null, Now));
        Assert.AreEqual("command.invalid_transition", ex.Code);
    }

    [TestMethod]
    public void Cancel_InFlightOnly()
    {
        var command = NewCommand();
        command.Cancel(Now);
        Assert.AreEqual(AgentCommandStatus.Cancelled, command.Status);

        var ex = Assert.ThrowsExactly<DomainException>(() => command.Cancel(Now));
        Assert.AreEqual("command.invalid_transition", ex.Code);
    }

    [TestMethod]
    public void Cancel_OnceDelivered_Throws()
    {
        var command = NewCommand();
        command.MarkDelivered(Now);

        var ex = Assert.ThrowsExactly<DomainException>(() => command.Cancel(Now));

        Assert.AreEqual("command.invalid_transition", ex.Code);
        Assert.AreEqual(AgentCommandStatus.Delivered, command.Status);
    }

    [TestMethod]
    public void ExpiresAt_UsesDeliveryTimeWhenDelivered()
    {
        var command = NewCommand(timeoutSeconds: 60);
        Assert.AreEqual(Now.AddSeconds(60), command.ExpiresAt);

        command.MarkDelivered(Now.AddSeconds(30));
        Assert.AreEqual(Now.AddSeconds(90), command.ExpiresAt);
    }

    [TestMethod]
    public void Expire_MarksExpired()
    {
        var command = NewCommand();
        command.MarkDelivered(Now);

        command.Expire(Now.AddMinutes(10));

        Assert.AreEqual(AgentCommandStatus.Expired, command.Status);
    }
}
