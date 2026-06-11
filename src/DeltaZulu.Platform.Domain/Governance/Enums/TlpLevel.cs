namespace DeltaZulu.Platform.Domain.Governance.Enums;

/// <summary>
/// Traffic Light Protocol 2.0 classification levels (FIRST/CISA standard).
/// </summary>
public enum TlpLevel
{
    Clear = 0,
    Green = 1,
    Amber = 2,
    AmberStrict = 3,
    Red = 4,
}