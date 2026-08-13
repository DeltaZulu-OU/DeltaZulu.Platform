using DeltaZulu.Platform.Domain.Analytics.Execution;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Current Proton execution capability. This deliberately remains not-ready until the
/// runtime checklist and live integration coverage have been completed and reviewed.
/// </summary>
public sealed class ProtonExecutionReadiness : IProtonExecutionReadiness
{
    public bool IsExecutionValidated => false;

    public string Reason =>
        "Proton execution parity is disabled until the runtime readiness checklist and live integration tests are complete.";
}
