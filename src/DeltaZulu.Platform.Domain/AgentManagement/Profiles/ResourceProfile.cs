using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Profiles;

public sealed class ResourceProfile : Entity<ResourceProfileId>
{
    public TenantId TenantId { get; }
    public string Name { get; private set; }
    public ProfileOrigin Origin { get; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ResourceProfile(
        ResourceProfileId id, TenantId tenantId, string name,
        ProfileOrigin origin, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Origin = origin;
        Enabled = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static ResourceProfile CreateDraft(
        ResourceProfileId id, TenantId tenantId, string name,
        ProfileOrigin origin, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("profile.name_empty", "Resource profile name must not be empty.");

        if (name.Length > 200)
            throw new DomainException("profile.name_too_long", "Resource profile name exceeds 200 characters.");

        return new ResourceProfile(id, tenantId, name, origin, now);
    }

    public static ResourceProfile Reconstitute(
        ResourceProfileId id, TenantId tenantId, string name,
        ProfileOrigin origin, bool enabled,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, tenantId, name, origin, createdAt) { Enabled = enabled, UpdatedAt = updatedAt };

    public void Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("profile.name_empty", "Resource profile name must not be empty.");

        if (newName.Length > 200)
            throw new DomainException("profile.name_too_long", "Resource profile name exceeds 200 characters.");

        Name = newName;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Enabled = false;
        UpdatedAt = now;
    }

    public void Enable(DateTimeOffset now)
    {
        Enabled = true;
        UpdatedAt = now;
    }
}
