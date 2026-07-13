using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Domain.AgentManagement.Agents;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class AgentCommandServiceTests
{
    private readonly TestClock _clock = new();
    private readonly FakeAgentRepository _agents = new();
    private readonly FakeAgentCommandRepository _commands = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AgentCommandService CreateService() => new(_commands, _agents, _unitOfWork, _clock);

    private Agent NewAgent()
    {
        var agent = Agent.Enroll(AgentId.New(), TenantId.Default, "cmd-host",
            ResourcePlatform.Windows, _clock.Now);
        _agents.Add(agent);
        return agent;
    }

    [TestMethod]
    public async Task Issue_CreatesPendingCommand()
    {
        var agent = NewAgent();

        var command = await CreateService().IssueAsync(
            agent.Id, AgentCommandType.CollectDiagnostics, "operator");

        Assert.AreEqual(AgentCommandStatus.Pending, command.Status);
        Assert.AreEqual("operator", command.RequestedBy);
        Assert.HasCount(1, _commands.Commands);
    }

    [TestMethod]
    public async Task Issue_UnknownAgent_Throws()
    {
        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateService().IssueAsync(AgentId.New(), AgentCommandType.FlushBuffer, null));
        Assert.AreEqual("agent.not_found", ex.Code);
    }

    [TestMethod]
    public async Task Cancel_PendingCommand()
    {
        var agent = NewAgent();
        var service = CreateService();
        var command = await service.IssueAsync(agent.Id, AgentCommandType.TestOutput, null);

        await service.CancelAsync(command.Id);

        Assert.AreEqual(AgentCommandStatus.Cancelled, _commands.Commands[command.Id].Status);
    }

    [TestMethod]
    public async Task ExpireOverdue_ExpiresOnlyPastTimeout()
    {
        var agent = NewAgent();
        var service = CreateService();
        var shortCommand = await service.IssueAsync(agent.Id, AgentCommandType.FlushBuffer, null, timeoutSeconds: 60);
        var longCommand = await service.IssueAsync(agent.Id, AgentCommandType.FlushBuffer, null, timeoutSeconds: 3600);

        _clock.Advance(TimeSpan.FromMinutes(5));
        var expired = await service.ExpireOverdueAsync(TenantId.Default);

        Assert.AreEqual(1, expired);
        Assert.AreEqual(AgentCommandStatus.Expired, _commands.Commands[shortCommand.Id].Status);
        Assert.AreEqual(AgentCommandStatus.Pending, _commands.Commands[longCommand.Id].Status);
    }
}
