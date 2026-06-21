using Microsoft.JSInterop;

namespace DeltaZulu.Blazor.Interop;

/// <summary>
/// Extension methods for safely calling JS interop during component lifecycle (init/dispose).
/// Absorbs the standard set of exceptions that indicate the circuit is gone or the operation
/// was cancelled, so callers don't repeat the same five-clause catch block.
/// </summary>
public static class JsLifecycleGuard
{
    public static async ValueTask<bool> TryInvokeVoidAsync(
        this IJSRuntime js,
        string identifier,
        params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (Exception ex) when (IsLifecycleException(ex))
        {
            return false;
        }
    }

    public static async ValueTask<(bool Success, T? Value)> TryInvokeAsync<T>(
        this IJSRuntime js,
        string identifier,
        params object?[] args)
    {
        try
        {
            return (true, await js.InvokeAsync<T>(identifier, args));
        }
        catch (Exception ex) when (IsLifecycleException(ex))
        {
            return (false, default);
        }
    }

    private static bool IsLifecycleException(Exception ex) =>
        ex is JSDisconnectedException
            or TaskCanceledException
            or OperationCanceledException
            or JSException
            or InvalidOperationException
            or ObjectDisposedException;
}
