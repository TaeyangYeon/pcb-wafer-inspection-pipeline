using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.UI.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IInspectionRepository _repository;

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    [ObservableProperty]
    private string resultFilter = "All";

    [ObservableProperty]
    private string exportPath;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int okCount;

    [ObservableProperty]
    private int ngCount;

    [ObservableProperty]
    private int anomalyCount;

    public List<string> ResultFilterOptions { get; } = new() { "All", "OK", "NG", "ANOMALY" };
    
    public ObservableCollection<InspectionRecord> Records { get; } = new();

    public HistoryViewModel(IInspectionRepository repository)
    {
        _repository = repository;
        
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktopPath = Path.Combine(homeDir, "Desktop");
        ExportPath = Path.Combine(desktopPath, "inspection_export.csv");

        _ = Task.Run(LoadAllRecordsAsync);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Searching records...";

            var filterResult = ResultFilter == "All" ? null : ResultFilter;
            var records = await _repository.GetRecordsAsync(FromDate, ToDate, filterResult);

            Records.Clear();
            var sortedRecords = records.OrderByDescending(r => r.Timestamp);
            
            foreach (var record in sortedRecords)
            {
                Records.Add(record);
            }

            UpdateSummary();
            StatusMessage = $"Found {Records.Count} records";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FromDate = null;
        ToDate = null;
        ResultFilter = "All";
        
        _ = Task.Run(LoadAllRecordsAsync);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(ExportPath))
            {
                StatusMessage = "Error: Export path is required";
                return;
            }

            IsBusy = true;
            StatusMessage = "Exporting records...";

            var directory = Path.GetDirectoryName(ExportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await _repository.ExportToCsvAsync(ExportPath);
            
            StatusMessage = $"Exported {Records.Count} records to {ExportPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAllRecordsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading records...";

            var records = await _repository.GetRecordsAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Records.Clear();
                var sortedRecords = records.OrderByDescending(r => r.Timestamp);
                
                foreach (var record in sortedRecords)
                {
                    Records.Add(record);
                }

                UpdateSummary();
                StatusMessage = $"Loaded {Records.Count} records";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Error loading records: {ex.Message}";
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
            });
        }
    }

    private void UpdateSummary()
    {
        TotalCount = Records.Count;
        OkCount = Records.Count(r => r.FinalResult == "OK");
        NgCount = Records.Count(r => r.FinalResult == "NG");
        AnomalyCount = Records.Count(r => r.FinalResult == "ANOMALY");
    }

    public string GetImageFileName(string imagePath)
    {
        return Path.GetFileName(imagePath);
    }

    public int GetDefectCount(InspectionRecord record)
    {
        return record.DefectDetails?.Count ?? 0;
    }
}