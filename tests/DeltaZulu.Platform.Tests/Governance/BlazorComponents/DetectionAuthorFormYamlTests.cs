using Bunit;
using DeltaZulu.Platform.Tests.Components;
using DeltaZulu.Platform.Web.Analytics.Services;
using DeltaZulu.Platform.Web.Governance.Components.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Tests.Governance.BlazorComponents;

[TestClass]
public sealed class DetectionAuthorFormYamlTests
{
    private const string ScheduledYaml = """
        id: dz-test-scheduled
        title: Test Scheduled Detection
        severity: Medium
        confidence: Medium
        risk_score: 60
        content_type: detection
        query_language: kql
        trigger_type: scheduled
        schedule: '0 3 * * 1'
        lookback: 1d
        max_alerts_per_run: 100
        description: >-
          Detects test patterns in process telemetry.
        query: |
          ProcessEvent
          | where Timestamp > ago(1d)
          | sort by Timestamp desc
        tactics:
          - Command and Control
          - Execution
        techniques:
          - T1105
        entity_mappings:
          - type: host
            field: DeviceName
          - type: account
            field: AccountName
        false_positive_notes:
          - Administrative activity may match.
        """;

    private const string NrtYaml = """
        id: dz-test-nrt
        title: Test NRT Detection
        severity: High
        confidence: High
        risk_score: 80
        content_type: detection
        query_language: kql
        trigger_type: nrt
        threshold: 5
        description: >-
          High-fidelity NRT indicator.
        query: |
          ProcessEvent
          | where Timestamp > ago(1h)
          | limit 10
        """;

    private const string OldScheduleFormatYaml = """
        id: dz-old-format
        title: Old Format Detection
        severity: low
        confidence: low
        risk_score: 20
        trigger_type: scheduled
        schedule:
          type: scheduled
          expression: '30 6 * * *'
          materialization: none
        lookback: 4h
        max_alerts_per_run: 50
        description: >-
          An older detection using nested schedule format.
        query: |
          NetworkSession
          | limit 5
        """;

    private static BunitContext CreateContext()
    {
        var context = MudBlazorTestContext.Create();
        context.Services.AddScoped<WidgetEditorInterop>();
        return context;
    }

    [TestMethod]
    public async Task GetYamlAsync_DefaultState_EmitsRequiredStructuralFields()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>(p => p
            .Add(c => c.DetectionSlug, "dz-new-detection")
            .Add(c => c.DetectionTitle, "New Detection"));

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("id: \"dz-new-detection\"", yaml);
        Assert.Contains("title: \"New Detection\"", yaml);
        Assert.Contains("content_type: detection", yaml);
        Assert.Contains("query_language: kql", yaml);
        Assert.Contains("trigger_type: nrt", yaml);
        Assert.Contains("query: |", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_ScheduledDetection_TriggerFieldsRoundTrip()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("trigger_type: scheduled", yaml);
        Assert.Contains("schedule:", yaml);
        Assert.Contains("0 3 * * 1", yaml);
        Assert.Contains("lookback: 1d", yaml);
        Assert.Contains("max_alerts_per_run: 100", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_NrtDetection_TriggerFieldsRoundTrip()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(NrtYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("trigger_type: nrt", yaml);
        Assert.Contains("threshold: 5", yaml);
        Assert.DoesNotContain("schedule:", yaml, "NRT detection should not include schedule field");
        Assert.DoesNotContain("lookback:", yaml, "NRT detection should not include lookback field");
    }

    [TestMethod]
    public async Task LoadFromYaml_IdentityAndScoringFields_RoundTrip()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("dz-test-scheduled", yaml);
        Assert.Contains("Test Scheduled Detection", yaml);
        Assert.Contains("severity: medium", yaml);
        Assert.Contains("confidence: medium", yaml);
        Assert.Contains("risk_score: 60", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_KqlQuery_RoundTripped()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("query: |", yaml);
        Assert.Contains("ProcessEvent", yaml);
        Assert.Contains("sort by Timestamp desc", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_FoldedDescription_ParsedAndEmitted()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("description: >-", yaml);
        Assert.Contains("Detects test patterns in process telemetry.", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_EntityMappings_ParsedAndRoundTripped()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("entity_mappings:", yaml);
        Assert.Contains("type: host", yaml);
        Assert.Contains("field: DeviceName", yaml);
        Assert.Contains("type: account", yaml);
        Assert.Contains("field: AccountName", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_TacticsAndTechniques_RoundTripped()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("tactics:", yaml);
        Assert.Contains("- Command and Control", yaml);
        Assert.Contains("- Execution", yaml);
        Assert.Contains("techniques:", yaml);
        Assert.Contains("- T1105", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_FalsePositiveNotes_RoundTripped()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("false_positive_notes:", yaml);
        Assert.Contains("Administrative activity may match.", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_OldNestedScheduleFormat_ParsesExpressionFromNested()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(OldScheduleFormatYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("trigger_type: scheduled", yaml);
        Assert.Contains("30 6 * * *", yaml);
        Assert.Contains("lookback: 4h", yaml);
        Assert.Contains("max_alerts_per_run: 50", yaml);
    }

    [TestMethod]
    public async Task LoadFromYaml_NrtHighSeverity_SeverityLowercased()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(NrtYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        // Severity values from YAML ("High") are stored lowercase
        Assert.Contains("severity: high", yaml);
        Assert.Contains("confidence: high", yaml);
        Assert.DoesNotContain("severity: High", yaml, "Severity should be stored and emitted in lowercase");
    }

    [TestMethod]
    public async Task LoadFromYaml_CalledTwice_SecondLoadOverwritesFirst()
    {
        await using var context = CreateContext();

        var cut = context.Render<DetectionAuthorForm>();
        cut.Instance.LoadFromYaml(ScheduledYaml);
        cut.Instance.LoadFromYaml(NrtYaml);

        var yaml = await cut.Instance.GetYamlAsync();

        Assert.Contains("dz-test-nrt", yaml);
        Assert.Contains("trigger_type: nrt", yaml);
        Assert.DoesNotContain("dz-test-scheduled", yaml, "Second load should replace id from first load");
    }
}
