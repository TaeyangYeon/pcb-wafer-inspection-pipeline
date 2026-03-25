using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

/// <summary>
/// Service for managing application settings persistence using JSON files.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "pcb-wafer-inspection");

        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _settingsFilePath = Path.Combine(appDataPath, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return GetDefaults();

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return GetDefaults();

            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            return settings ?? GetDefaults();
        }
        catch
        {
            return GetDefaults();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public AppSettings GetDefaults()
    {
        return new AppSettings();
    }
}