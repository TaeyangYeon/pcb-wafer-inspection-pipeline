using System.Threading.Tasks;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Service for managing application settings persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the application settings from persistent storage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the loaded settings.</returns>
    /// <remarks>
    /// Returns default settings if no saved settings exist or if loading fails.
    /// </remarks>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Saves the application settings to persistent storage.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when settings is null.</exception>
    Task SaveAsync(AppSettings settings);

    /// <summary>
    /// Gets the default application settings.
    /// </summary>
    /// <returns>A new instance of AppSettings with default values.</returns>
    AppSettings GetDefaults();
}