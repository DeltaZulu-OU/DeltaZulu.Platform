using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed class DaemonConfigService(
    IDaemonConfigPolicyRepository policyRepo,
    IDaemonConfigVersionRepository versionRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<DaemonConfigPolicy> CreatePolicyAsync(
        TenantId tenantId, string name, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var policy = DaemonConfigPolicy.Create(ConfigPolicyId.New(), tenantId, name, now);
        policyRepo.Add(policy);
        await unitOfWork.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<DaemonConfigPolicy?> GetPolicyByIdAsync(ConfigPolicyId id, CancellationToken ct = default) =>
        await policyRepo.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<DaemonConfigPolicy>> ListPoliciesAsync(TenantId tenantId, CancellationToken ct = default) =>
        await policyRepo.ListByTenantAsync(tenantId, ct);

    public async Task<DaemonConfigVersion> CreateVersionAsync(
        ConfigPolicyId configPolicyId, int sequenceNumber,
        PipelineConfig pipeline, BufferConfig buffer, RelpConfig relp,
        TlsConfig tls, DiagnosticsConfig diagnostics, string profilesPath,
        string contentHash, string? author, CancellationToken ct = default)
    {
        _ = await policyRepo.GetByIdAsync(configPolicyId, ct)
            ?? throw new DomainException("configpolicy.not_found", $"Config policy {configPolicyId} not found.");

        var now = timeProvider.GetUtcNow();
        var version = DaemonConfigVersion.Create(
            ConfigVersionId.New(), configPolicyId, sequenceNumber,
            pipeline, buffer, relp, tls, diagnostics, profilesPath,
            contentHash, author, now);

        versionRepo.Add(version);
        await unitOfWork.SaveChangesAsync(ct);
        return version;
    }

    public async Task MarkValidatedAsync(ConfigVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("configversion.not_found", $"Config version {id} not found.");

        var findings = Validation.DaemonConfigValidator.Validate(version);
        if (Validation.DaemonConfigValidator.HasBlockingFailures(findings))
        {
            var failures = findings
                .Where(f => f.IsBlocking)
                .Select(f => $"{f.ArtifactType}.{f.FieldPath}: {f.Message}");
            throw new DomainException("configversion.validation_failed",
                $"Config version failed validation: {string.Join(" | ", failures)}");
        }

        var now = timeProvider.GetUtcNow();
        version.MarkValidated(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(ConfigVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("configversion.not_found", $"Config version {id} not found.");

        var now = timeProvider.GetUtcNow();
        version.Publish(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DaemonConfigVersion>> ListVersionsAsync(
        ConfigPolicyId configPolicyId, CancellationToken ct = default) =>
        await versionRepo.ListByConfigIdAsync(configPolicyId, ct);
}
