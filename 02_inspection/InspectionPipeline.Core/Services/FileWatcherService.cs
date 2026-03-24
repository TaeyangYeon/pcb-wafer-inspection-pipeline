using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InspectionPipeline.Core.Interfaces;

namespace InspectionPipeline.Core.Services;

/// <summary>
/// Service for monitoring a directory for new image files using FileSystemWatcher.
/// Handles macOS FileSystemWatcher quirks with deduplication and debouncing.
/// </summary>
public class FileWatcherService : IFileWatcherService, IDisposable
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly HashSet<string> _recentlyProcessed;
    private readonly object _lock = new();
    private readonly Timer _cleanupTimer;
    
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recentlyProcessed = new HashSet<string>();
        
        // Timer to clean up old entries from _recentlyProcessed every 5 seconds
        _cleanupTimer = new Timer(CleanupRecentlyProcessed, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public event EventHandler<string>? ImageDetected;

    public bool IsRunning { get; private set; }

    public void Start(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be null or empty.", nameof(folderPath));

        if (!Directory.Exists(folderPath))
            throw new ArgumentException($"Directory does not exist: {folderPath}", nameof(folderPath));

        if (IsRunning)
            throw new InvalidOperationException("File watcher service is already running.");

        try
        {
            _watcher = new FileSystemWatcher(folderPath)
            {
                Filter = "*.*",
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };

            _watcher.Created += OnFileCreated;
            _watcher.EnableRaisingEvents = true;
            
            IsRunning = true;
            _logger.LogInformation("File watcher started monitoring: {FolderPath}", folderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file watcher for path: {FolderPath}", folderPath);
            _watcher?.Dispose();
            _watcher = null;
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        try
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Dispose();
                _watcher = null;
            }

            IsRunning = false;
            
            lock (_lock)
            {
                _recentlyProcessed.Clear();
            }

            _logger.LogInformation("File watcher stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping file watcher");
        }
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            var filePath = e.FullPath;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Filter for image files only
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                return;

            // Check for recent processing to avoid duplicates (macOS issue)
            lock (_lock)
            {
                if (_recentlyProcessed.Contains(filePath))
                {
                    _logger.LogDebug("File already processed recently, ignoring: {FilePath}", filePath);
                    return;
                }
                _recentlyProcessed.Add(filePath);
            }

            _logger.LogDebug("File creation detected: {FilePath}", filePath);

            // Debounce: wait 500ms to ensure file is fully written
            await Task.Delay(500);

            // Verify file still exists and is accessible
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File no longer exists after debounce: {FilePath}", filePath);
                return;
            }

            // Fire the event in a thread-safe manner
            var handler = ImageDetected;
            if (handler != null)
            {
                _logger.LogInformation("Image detected: {FilePath}", filePath);
                handler.Invoke(this, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file creation event: {FilePath}", e.FullPath);
        }
    }

    private void CleanupRecentlyProcessed(object? state)
    {
        try
        {
            lock (_lock)
            {
                // For simplicity, clear all entries periodically
                // In a production system, you might want to track timestamps and remove only old entries
                if (_recentlyProcessed.Count > 100)
                {
                    _logger.LogDebug("Cleaning up recently processed files cache");
                    _recentlyProcessed.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of recently processed files");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _cleanupTimer?.Dispose();
        _disposed = true;
        
        _logger.LogDebug("FileWatcherService disposed");
    }
}