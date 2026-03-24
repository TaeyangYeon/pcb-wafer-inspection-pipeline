using Microsoft.Extensions.DependencyInjection;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.Core.Extensions;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all core inspection pipeline services to the DI container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddInspectionPipelineServices(this IServiceCollection services)
    {
        services.AddSingleton<IDriftMonitor, DriftMonitor>();
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IImageOverlayRenderer, ImageOverlayRenderer>();
        
        return services;
    }
}