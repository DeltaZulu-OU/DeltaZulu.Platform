using DeltaZulu.Blazor.Interop;
using Microsoft.JSInterop;

namespace DeltaZulu.Platform.Web.Analytics.Services;

/// <summary>
/// Typed wrapper for the huntingDashboardWidgetEditor JS namespace (dashboard-widget-editor.js).
/// Hides the raw JS identifier strings from components and controller code.
/// Methods propagate JS exceptions so callers can apply component-specific recovery logic.
/// </summary>
public sealed class WidgetEditorInterop(IJSRuntime js)
{
    public ValueTask<bool> InitializeAsync(string elementId, string value, string language) =>
        js.InvokeAsync<bool>("huntingDashboardWidgetEditor.initialize", elementId, value, language);

    public ValueTask<bool> SetLanguageAsync(string elementId, string language) =>
        js.InvokeAsync<bool>("huntingDashboardWidgetEditor.setLanguage", elementId, language);

    public ValueTask<string> GetValueAsync(string elementId) =>
        js.InvokeAsync<string>("huntingDashboardWidgetEditor.getValue", elementId);

    public ValueTask SetValueAsync(string elementId, string value) =>
        js.InvokeVoidAsync("huntingDashboardWidgetEditor.setValue", elementId, value);

    public ValueTask DisposeEditorAsync(string elementId) =>
        js.InvokeVoidAsync("huntingDashboardWidgetEditor.dispose", elementId);

    /// <summary>
    /// Disposes the editor instance, swallowing any lifecycle exceptions (disconnected circuit,
    /// cancellation, etc.). Safe to call from component DisposeAsync / cleanup paths.
    /// </summary>
    public ValueTask<bool> TryDisposeAsync(string elementId) =>
        js.TryInvokeVoidAsync("huntingDashboardWidgetEditor.dispose", elementId);
}
