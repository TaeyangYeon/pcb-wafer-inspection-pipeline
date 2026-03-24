using System;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.UI.DependencyInjection;

/// <summary>
/// Extension methods for configuring dependency injection services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all inspection pipeline services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="apiBaseUrl">The base URL for the inspection API client.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddInspectionPipelineServices(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new ArgumentException("API base URL cannot be null or empty.", nameof(apiBaseUrl));

        // Configure Entity Framework with SQLite
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "pcb-wafer-inspection",
            "inspection.db");

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!Directory.Exists(dbDirectory))
            Directory.CreateDirectory(dbDirectory!);

        services.AddDbContext<InspectionDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repository services
        services.AddScoped<IInspectionRepository, InspectionRepository>();

        // API client service with HttpClient configuration
        services.AddSingleton<IInspectionApiClient>(provider =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            return new InspectionApiClient(httpClient);
        });

        // Core services as singletons
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IDriftMonitor, DriftMonitor>();
        services.AddSingleton<IAlarmService, AlarmService>();

        return services;
    }
}