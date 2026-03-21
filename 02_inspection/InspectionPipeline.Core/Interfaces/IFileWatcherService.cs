using System;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Service for monitoring a directory for new image files and raising events when images are detected.
/// </summary>
public interface IFileWatcherService
{
    /// <summary>
    /// Event triggered when a new image file is detected in the monitored directory.
    /// </summary>
    /// <remarks>
    /// The string parameter contains the full file path of the detected image.
    /// </remarks>
    event EventHandler<string> ImageDetected;

    /// <summary>
    /// Starts monitoring the specified directory for new image files.
    /// </summary>
    /// <param name="folderPath">The directory path to monitor for new images.</param>
    /// <exception cref="ArgumentException">Thrown when the folder path is null, empty, or does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service is already running.</exception>
    void Start(string folderPath);

    /// <summary>
    /// Stops monitoring the directory for new image files.
    /// </summary>
    /// <remarks>
    /// Safe to call multiple times. Does nothing if the service is not currently running.
    /// </remarks>
    void Stop();

    /// <summary>
    /// Gets a value indicating whether the file watcher service is currently running.
    /// </summary>
    bool IsRunning { get; }
}