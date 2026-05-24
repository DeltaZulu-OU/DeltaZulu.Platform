namespace Hunting.Web.Services;

/// <summary>
/// Per-circuit channel that lets the SchemaBrowser (rendered in the layout
/// sidebar) push KQL text into the editor on the routed page. The layout and
/// the routed page are siblings in the render tree, so an EventCallback cannot
/// bridge them directly. Registered scoped so each Blazor Server circuit gets
/// its own instance.
/// </summary>
public sealed class EditorBus
{
    public event Action<string>? InsertRequested;

    public void RequestInsert(string kql) => InsertRequested?.Invoke(kql);
}