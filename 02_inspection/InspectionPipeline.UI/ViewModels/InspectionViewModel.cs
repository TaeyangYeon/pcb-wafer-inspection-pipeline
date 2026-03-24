using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.UI.ViewModels;

public partial class InspectionViewModel : ObservableObject
{
    private readonly IInspectionApiClient _apiClient;
    private readonly IInspectionRepository _repository;
    private readonly IImageOverlayRenderer _overlayRenderer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string imagePath = "";

    [ObservableProperty]
    private Bitmap? originalImageBitmap;

    [ObservableProperty]
    private Bitmap? resultImageBitmap;

    [ObservableProperty]
    private string finalResult = "";

    [ObservableProperty]
    private string stageUsed = "";

    [ObservableProperty]
    private double anomalyScore;

    [ObservableProperty]
    private double inferenceTimeMs;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private bool isRunning;

    public ObservableCollection<DetectionItem> Detections { get; } = new();

    public InspectionViewModel(
        IInspectionApiClient apiClient,
        IInspectionRepository repository,
        IImageOverlayRenderer overlayRenderer)
    {
        _apiClient = apiClient;
        _repository = repository;
        _overlayRenderer = overlayRenderer;
    }

    [RelayCommand]
    private async Task OpenImageAsync()
    {
        try
        {
            var app = App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            if (mainWindow == null)
                return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Image File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tiff" }
                    }
                }
            });

            if (files.Count > 0 && files[0].Path.LocalPath is string localPath)
            {
                await LoadImageAsync(localPath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task LoadImageAsync(string path)
    {
        try
        {
            ImagePath = path;
            StatusMessage = "Loading image...";

            await using var stream = File.OpenRead(path);
            OriginalImageBitmap = await Task.Run(() => new Bitmap(stream));

            StatusMessage = "Image loaded";
            ClearResults();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading image: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunInspectionAsync()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            StatusMessage = "Error: No image selected";
            return;
        }

        if (!await _semaphore.WaitAsync(100))
        {
            StatusMessage = "Error: Inspection already running";
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            IsRunning = true;
            StatusMessage = "Running inspection...";
            ClearResults();

            var result = await Task.Run(async () =>
                await _apiClient.InspectAsync(ImagePath, _cts.Token), _cts.Token);

            await ProcessResultAsync(result);

            StatusMessage = "Inspection complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Inspection cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            _semaphore.Release();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    private async Task ProcessResultAsync(PipelineResult result)
    {
        try
        {
            FinalResult = result.FinalDecision;
            StageUsed = result.DecisionStage;
            AnomalyScore = result.AnomalyResult.Score;
            InferenceTimeMs = result.TotalTimeMs;

            Detections.Clear();
            if (result.SegmentationResult?.Detections != null)
            {
                foreach (var detection in result.SegmentationResult.Detections)
                {
                    Detections.Add(new DetectionItem
                    {
                        ClassName = detection.ClassName,
                        Confidence = detection.Confidence,
                        BoundingBox = $"({detection.BoundingBox.X:F0}, {detection.BoundingBox.Y:F0}, " +
                                     $"{detection.BoundingBox.Width:F0}, {detection.BoundingBox.Height:F0})"
                    });
                }
            }

            await GenerateResultImageAsync(result);

            await SaveRecordAsync(result);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error processing result: {ex.Message}";
        }
    }

    private async Task GenerateResultImageAsync(PipelineResult result)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(ImagePath);
            var renderedBytes = await Task.Run(() => _overlayRenderer.RenderCombined(imageBytes, result));

            await using var stream = new MemoryStream(renderedBytes);
            ResultImageBitmap = await Task.Run(() => new Bitmap(stream));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error rendering result image: {ex.Message}";
        }
    }

    private async Task SaveRecordAsync(PipelineResult result)
    {
        try
        {
            var record = new InspectionRecord
            {
                Timestamp = result.Timestamp,
                ImagePath = result.ImagePath,
                FinalResult = result.FinalDecision,
                StageUsed = result.DecisionStage,
                AnomalyScore = result.AnomalyResult.Score,
                InferenceTimeMs = result.TotalTimeMs,
                ModelVersionId = 1
            };

            if (result.SegmentationResult?.Detections != null)
            {
                foreach (var detection in result.SegmentationResult.Detections)
                {
                    record.DefectDetails.Add(new DefectDetail
                    {
                        ClassName = detection.ClassName,
                        Confidence = detection.Confidence,
                        BboxX = detection.BoundingBox.X,
                        BboxY = detection.BoundingBox.Y,
                        BboxW = detection.BoundingBox.Width,
                        BboxH = detection.BoundingBox.Height,
                        MaskArea = detection.Mask.Area
                    });
                }
            }

            await _repository.SaveRecordAsync(record);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save record to database: {ex.Message}");
        }
    }

    private void ClearResults()
    {
        FinalResult = "";
        StageUsed = "";
        AnomalyScore = 0;
        InferenceTimeMs = 0;
        ResultImageBitmap?.Dispose();
        ResultImageBitmap = null;
        Detections.Clear();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        OriginalImageBitmap?.Dispose();
        ResultImageBitmap?.Dispose();
        _semaphore?.Dispose();
    }
}

public class DetectionItem
{
    public required string ClassName { get; set; }
    public required double Confidence { get; set; }
    public required string BoundingBox { get; set; }
}