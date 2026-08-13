using DeltaZulu.Platform.Application.AgentManagement.Services;
using DeltaZulu.Platform.Application.AgentManagement.Validation;
using DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;
using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Tests.AgentManagement.Application;

[TestClass]
public sealed class ValidationGatingTests
{
    private readonly TestClock _clock = new();
    private readonly FakeResourceProfileRepository _profiles = new();
    private readonly FakeResourceProfileVersionRepository _profileVersions = new();
    private readonly FakeDaemonConfigPolicyRepository _configPolicies = new();
    private readonly FakeDaemonConfigVersionRepository _configVersions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private static readonly IProfileValidationCheck[] Checks =
    [
        new ProfileSchemaCheck(),
        new ResourceDescriptorCheck(),
        new InputContractCheck(),
        new OutputContractCheck(),
    ];

    private ResourceProfileService CreateProfileService() => new(
        _profiles, _profileVersions, _unitOfWork, _clock,
        new ProfileValidationPipelineRunner(Checks), Checks);

    private DaemonConfigService CreateConfigService() => new(
        _configPolicies, _configVersions, _unitOfWork, _clock);

    private ResourceProfileVersion AddDraftProfileVersion(InputContract inputContract)
    {
        var version = ResourceProfileVersion.CreateDraft(
            ProfileVersionId.New(), ResourceProfileId.New(), 1, "1.0", true, false,
            new ResourceDescriptor("Windows", "EventLog", null, "Security", null, null, null),
            inputContract,
            new OutputContract("forward", "ndjson", true, true, true, true, OnNoMatchBehavior.Keep),
            null, [], "hash", "test", _clock.Now);
        _profileVersions.Add(version);
        return version;
    }

    [TestMethod]
    public async Task MarkValidated_ValidProfileVersion_Transitions()
    {
        var version = AddDraftProfileVersion(new InputContract("SecurityEvent", "bronze"));

        await CreateProfileService().MarkValidatedAsync(version.Id, TestContext.CancellationToken);

        Assert.AreEqual(ProfileState.Validated, _profileVersions.Versions[version.Id].State);
    }

    [TestMethod]
    public async Task MarkValidated_BlockingFinding_IsRejectedAndStaysDraft()
    {
        var version = AddDraftProfileVersion(new InputContract("", ""));

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateProfileService().MarkValidatedAsync(version.Id, TestContext.CancellationToken));

        Assert.AreEqual("profileversion.validation_failed", ex.Code);
        Assert.Contains("Input table name is required", ex.Message);
        Assert.AreEqual(ProfileState.Draft, _profileVersions.Versions[version.Id].State);
    }

    [TestMethod]
    public async Task MarkValidated_ValidConfigVersion_Transitions()
    {
        var version = TestData.DraftConfigVersion(ConfigPolicyId.New(), 1, _clock.Now);
        _configVersions.Add(version);

        await CreateConfigService().MarkValidatedAsync(version.Id, TestContext.CancellationToken);

        Assert.AreEqual(ProfileState.Validated, _configVersions.Versions[version.Id].State);
    }

    [TestMethod]
    public async Task MarkValidated_ConfigWithThumbprintModeButNoThumbprints_IsRejected()
    {
        var version = TestData.DraftConfigVersion(
            ConfigPolicyId.New(), 1, _clock.Now,
            tls: new TlsConfig(true, TlsValidationMode.Thumbprint, null, false, null, null, null));
        _configVersions.Add(version);

        var ex = await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateConfigService().MarkValidatedAsync(version.Id, TestContext.CancellationToken));

        Assert.AreEqual("configversion.validation_failed", ex.Code);
        Assert.AreEqual(ProfileState.Draft, _configVersions.Versions[version.Id].State);
    }

    [TestMethod]
    public void DaemonConfigValidator_MemoryExceedingDisk_IsBlocking()
    {
        var buffer = BufferConfig.DefaultEndpoint() with
        {
            MaxMemoryBytes = 10,
            MaxDiskBytes = 5,
            MaxChunkBytes = 1,
        };
        var version = TestData.DraftConfigVersion(ConfigPolicyId.New(), 1, _clock.Now, buffer: buffer);

        var findings = DaemonConfigValidator.Validate(version);

        Assert.IsTrue(DaemonConfigValidator.HasBlockingFailures(findings));
        Assert.Contains("must not exceed the disk buffer limit",
            string.Join(" | ", findings.Select(f => f.Message)));
    }

    [TestMethod]
    public void DaemonConfigValidator_RelpTlsWithoutTlsConfig_IsBlocking()
    {
        var version = TestData.DraftConfigVersion(
            ConfigPolicyId.New(), 1, _clock.Now,
            relp: new RelpConfig(true, [], "utf-8", "tcp"),
            tls: new TlsConfig(false, TlsValidationMode.System, null, false, null, null, null));

        var findings = DaemonConfigValidator.Validate(version);

        Assert.IsTrue(DaemonConfigValidator.HasBlockingFailures(findings));
    }

    [TestMethod]
    public void DaemonConfigValidator_DisabledCertValidation_IsWarningOnly()
    {
        var version = TestData.DraftConfigVersion(
            ConfigPolicyId.New(), 1, _clock.Now,
            tls: new TlsConfig(true, TlsValidationMode.None, null, false, null, null, null));

        var findings = DaemonConfigValidator.Validate(version);

        Assert.IsFalse(DaemonConfigValidator.HasBlockingFailures(findings));
        Assert.IsTrue(findings.Any(f => f.Severity == ValidationSeverity.Warning));
    }

    public TestContext TestContext { get; set; }
}
