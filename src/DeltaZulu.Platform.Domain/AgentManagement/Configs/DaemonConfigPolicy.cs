using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Configs;

public sealed class DaemonConfigPolicy : Entity<ConfigPolicyId>
{
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DaemonConfigPolicy(
        ConfigPolicyId id, TenantId tenantId, string name, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static DaemonConfigPolicy Create(
        ConfigPolicyId id, TenantId tenantId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("configpolicy.name_empty", "Config policy name must not be empty.");

        if (name.Length > 200)
            throw new DomainException("configpolicy.name_too_long", "Config policy name exceeds 200 characters.");

        return new DaemonConfigPolicy(id, tenantId, name, now);
    }

    public static DaemonConfigPolicy Reconstitute(
        ConfigPolicyId id, TenantId tenantId, string name,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, name, createdAt) { UpdatedAt = updatedAt };

    public void Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("configpolicy.name_empty", "Config policy name must not be empty.");

        if (newName.Length > 200)
            throw new DomainException("configpolicy.name_too_long", "Config policy name exceeds 200 characters.");

        Name = newName;
        UpdatedAt = now;
    }
}
