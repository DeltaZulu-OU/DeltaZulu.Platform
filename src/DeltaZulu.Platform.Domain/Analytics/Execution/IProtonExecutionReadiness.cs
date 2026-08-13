namespace DeltaZulu.Platform.Domain.Analytics.Execution;

/// <summary>Reports whether Proton may participate in result-parity validation.</summary>
/// <remarks>
/// Compilation or deployment support alone does not imply execution readiness. Consumers
/// must check this capability before adding a Proton leg to cross-backend validation.
/// </remarks>
public interface IProtonExecutionReadiness
{
    /// <summary>Gets whether live Proton execution has passed the platform readiness criteria.</summary>
    bool IsExecutionValidated { get; }

    /// <summary>Explains the current readiness state for validation diagnostics.</summary>
    string Reason { get; }
}
