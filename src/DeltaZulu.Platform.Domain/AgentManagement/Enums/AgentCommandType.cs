namespace DeltaZulu.Platform.Domain.AgentManagement.Enums;

/// <summary>
/// The complete allowlist of remote operations an agent can be asked to perform.
/// Deliberately closed: no arbitrary shell, script execution, or free-form local
/// queries — new operations require a new enum member and agent support.
/// </summary>
public enum AgentCommandType
{
    ReloadConfiguration,
    TestOutput,
    FlushBuffer,
    CollectDiagnostics,
    RestartService
}
