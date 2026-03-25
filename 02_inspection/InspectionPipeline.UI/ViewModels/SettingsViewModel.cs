using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IInspectionApiClient _apiClient;
    private readonly IFileWatcherService _fileWatcherService;

    [ObservableProperty]
    private string _apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string _watchFolderPath = string.Empty;

    [ObservableProperty]
    private double _anomalyThreshold = 0.5;

    [ObservableProperty]
    private double _yoloConfThreshold = 0.25;

    [ObservableProperty]
    private bool _autoSaveNg = true;

    [ObservableProperty]
    private string _ngSavePath = string.Empty;

    [ObservableProperty]
    private string _patchcoreModelDir = string.Empty;

    [ObservableProperty]
    private string _yoloOnnxPath = string.Empty;

    [ObservableProperty]
    private bool _isWatcherRunning = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isTestingConnection = false;

    public SettingsViewModel(
        ISettingsService settingsService,
        IInspectionApiClient apiClient,
        IFileWatcherService fileWatcherService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _fileWatcherService = fileWatcherService ?? throw new ArgumentNullException(nameof(fileWatcherService));

        _fileWatcherService.ImageDetected += OnImageDetected;

        LoadSettingsAsync();
        UpdateWatcherStatus();
    }

    private void OnImageDetected(object? sender, string imagePath)
    {
        StatusMessage = $"Image detected: {Path.GetFileName(imagePath)}";
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            LoadFromSettings(settings);
            StatusMessage = "Settings loaded successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private void LoadFromSettings(AppSettings settings)
    {
        ApiBaseUrl = settings.ApiBaseUrl;
        WatchFolderPath = settings.WatchFolderPath;
        AnomalyThreshold = settings.AnomalyThreshold;
        YoloConfThreshold = settings.YoloConfThreshold;
        AutoSaveNg = settings.AutoSaveNg;
        NgSavePath = settings.NgSavePath;
        PatchcoreModelDir = settings.PatchcoreModelDir;
        YoloOnnxPath = settings.YoloOnnxPath;
    }

    private AppSettings CreateSettingsFromViewModel()
    {
        return new AppSettings
        {
            ApiBaseUrl = ApiBaseUrl,
            WatchFolderPath = WatchFolderPath,
            AnomalyThreshold = AnomalyThreshold,
            YoloConfThreshold = YoloConfThreshold,
            AutoSaveNg = AutoSaveNg,
            NgSavePath = NgSavePath,
            PatchcoreModelDir = PatchcoreModelDir,
            YoloOnnxPath = YoloOnnxPath
        };
    }

    private void UpdateWatcherStatus()
    {
        IsWatcherRunning = _fileWatcherService.IsRunning;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = CreateSettingsFromViewModel();
            await _settingsService.SaveAsync(settings);
            StatusMessage = "Settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        try
        {
            var defaults = _settingsService.GetDefaults();
            LoadFromSettings(defaults);
            StatusMessage = "Settings reset to defaults";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to reset settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            StatusMessage = "Please enter an API URL";
            return;
        }

        IsTestingConnection = true;
        
        try
        {
            var isHealthy = await _apiClient.IsServerHealthyAsync();
            StatusMessage = isHealthy 
                ? "Connection successful" 
                : $"Cannot connect to {ApiBaseUrl}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task ToggleWatcherAsync()
    {
        try
        {
            if (_fileWatcherService.IsRunning)
            {
                _fileWatcherService.Stop();
                StatusMessage = "File watcher stopped";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(WatchFolderPath) || !Directory.Exists(WatchFolderPath))
                {
                    StatusMessage = "Please specify a valid watch folder path";
                    return;
                }

                _fileWatcherService.Start(WatchFolderPath);
                StatusMessage = $"File watcher started for: {WatchFolderPath}";
            }

            UpdateWatcherStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to toggle watcher: {ex.Message}";
            UpdateWatcherStatus();
        }
    }
}