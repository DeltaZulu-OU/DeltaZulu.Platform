using DeltaZulu.Blazor.Components;
using MudBlazor;

namespace DeltaZulu.Workbench.Web;

/// <summary>
/// Compatibility shim — delegates to the shared <see cref="DeltaZuluTheme"/>.
/// </summary>
public static class WorkbenchTheme
{
    public static MudTheme Create() => DeltaZuluTheme.Create();
}
