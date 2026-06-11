using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Web.Governance.Services;

/// <summary>
/// Session-scoped POC user switcher used until authentication is introduced.
/// </summary>
public sealed class PocUserContext
{
    public PocUserContext()
    {
        Analyst = new PocUser(PocUserPersona.Analyst, UserId.New(), "POC Analyst", "analyst@workbench.local");
        Reviewer = new PocUser(PocUserPersona.Reviewer, UserId.New(), "POC Reviewer", "reviewer@workbench.local");
        CurrentPersona = PocUserPersona.Analyst;
    }

    public PocUser Analyst { get; }

    public PocUser Reviewer { get; }

    public PocUserPersona CurrentPersona { get; private set; }

    public PocUser CurrentUser => CurrentPersona switch
    {
        PocUserPersona.Reviewer => Reviewer,
        _ => Analyst,
    };

    public IReadOnlyList<PocUser> AvailableUsers => [Analyst, Reviewer];

    public void SwitchTo(PocUserPersona persona) => CurrentPersona = persona;
}

public enum PocUserPersona
{
    Analyst,
    Reviewer,
}

public sealed record PocUser(
    PocUserPersona Persona,
    UserId Id,
    string DisplayName,
    string Email);