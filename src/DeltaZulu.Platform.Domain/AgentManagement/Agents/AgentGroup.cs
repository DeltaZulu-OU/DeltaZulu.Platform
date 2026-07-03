using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Agents;

public sealed class AgentGroup : Entity<AgentGroupId>
{
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public string? SelectorsJson { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AgentGroup(
        AgentGroupId id, TenantId tenantId, string name, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static AgentGroup Create(
        AgentGroupId id, TenantId tenantId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("agentgroup.name_empty", "Agent group name must not be empty.");

        if (name.Length > 200)
            throw new DomainException("agentgroup.name_too_long", "Agent group name exceeds 200 characters.");

        return new AgentGroup(id, tenantId, name, now);
    }

    public static AgentGroup Reconstitute(
        AgentGroupId id, TenantId tenantId, string name,
        string? selectorsJson, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, name, createdAt) { SelectorsJson = selectorsJson, UpdatedAt = updatedAt };

    public void Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("agentgroup.name_empty", "Agent group name must not be empty.");

        if (newName.Length > 200)
            throw new DomainException("agentgroup.name_too_long", "Agent group name exceeds 200 characters.");

        Name = newName;
        UpdatedAt = now;
    }

    public void UpdateSelectors(string? selectorsJson, DateTimeOffset now)
    {
        SelectorsJson = selectorsJson;
        UpdatedAt = now;
    }
}
