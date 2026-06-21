using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Blazor.Interop;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDzBlazorInterop(this IServiceCollection services)
    {
        services.AddScoped<ClipboardService>();
        services.AddScoped<FileOperationsService>();
        return services;
    }
}
