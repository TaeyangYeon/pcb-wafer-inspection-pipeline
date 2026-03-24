using System;
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

public partial class DashboardViewModel : ObservableObject
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly IAlarmService _alarmService;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int ngCount;

    [ObservableProperty]
    private double ngRate;

    [ObservableProperty]
    private double avgInferenceMs;

    [ObservableProperty]
    private double avgAnomalyScore;

    [ObservableProperty]
    private bool isAlarming;

    [ObservableProperty]
    private string alarmMessage = "DRIFT DETECTED - Retraining Recommended";

    public ObservableCollection<InspectionRecord> RecentRecords { get; } = new();

    public DashboardViewModel(IInspectionRepository inspectionRepository, IAlarmService alarmService)
    {
        _inspectionRepository = inspectionRepository;
        _alarmService = alarmService;

        _alarmService.AlarmTriggered += OnAlarmTriggered;
        IsAlarming = _alarmService.IsAlarming;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (s, e) => await LoadDataAsync();
        _refreshTimer.Start();

        _ = Task.Run(LoadDataAsync);
    }

    [RelayCommand]
    private void DismissAlarm()
    {
        _alarmService.Reset();
        IsAlarming = false;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var today = DateTime.Today;
            var stats = await _inspectionRepository.GetStatsAsync(today);
            var recentRecords = await _inspectionRepository.GetRecordsAsync(
                from: DateTime.Now.AddHours(-24), 
                to: null, 
                resultFilter: null);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalCount = stats.TotalInspections;
                NgCount = stats.NgCount + stats.AnomalyCount;
                NgRate = TotalCount > 0 ? (double)NgCount / TotalCount * 100.0 : 0.0;
                AvgInferenceMs = stats.AverageInferenceTime;
                AvgAnomalyScore = stats.AverageAnomalyScore;

                RecentRecords.Clear();
                var recent = recentRecords
                    .OrderByDescending(r => r.Timestamp)
                    .Take(10);
                
                foreach (var record in recent)
                {
                    RecentRecords.Add(record);
                }

                IsAlarming = _alarmService.IsAlarming;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dashboard data: {ex.Message}");
        }
    }

    private void OnAlarmTriggered(object? sender, AlarmEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsAlarming = true;
            AlarmMessage = e.Message ?? "DRIFT DETECTED - Retraining Recommended";
        });
    }

    public string GetImageFileName(string imagePath)
    {
        return Path.GetFileName(imagePath);
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        if (_alarmService != null)
        {
            _alarmService.AlarmTriggered -= OnAlarmTriggered;
        }
    }
}