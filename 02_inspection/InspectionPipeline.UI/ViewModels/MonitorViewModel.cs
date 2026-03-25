using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace InspectionPipeline.UI.ViewModels;

public partial class MonitorViewModel : ObservableObject
{
    private readonly IDriftMonitor _driftMonitor;
    private readonly IInspectionRepository _repository;
    private readonly IAlarmService _alarmService;

    [ObservableProperty]
    private string driftStatus = "Insufficient Data";

    [ObservableProperty]
    private double ksStatistic;

    [ObservableProperty]
    private double pValue;

    [ObservableProperty]
    private string lastAnalyzedAt = "Never";

    [ObservableProperty]
    private bool hasBaseline;

    [ObservableProperty]
    private string baselineInfo = "No baseline set";

    [ObservableProperty]
    private bool isRetrainingRecommended;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private ISeries[] scoreSeries = Array.Empty<ISeries>();

    public MonitorViewModel(IDriftMonitor driftMonitor, IInspectionRepository repository, IAlarmService alarmService)
    {
        _driftMonitor = driftMonitor;
        _repository = repository;
        _alarmService = alarmService;

        HasBaseline = _driftMonitor.HasBaseline;
        UpdateBaselineInfo();

        _ = Task.Run(LoadScoreDataAsync);
    }

    [RelayCommand]
    private async Task RunAnalysisAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Running drift analysis...";

            if (!_driftMonitor.HasBaseline)
            {
                StatusMessage = "Error: No baseline set. Please set a baseline first.";
                return;
            }

            var scores = await _repository.GetAnomalyScoresAsync(100);
            if (scores.Count < 10)
            {
                StatusMessage = "Error: Insufficient data for analysis (need at least 10 scores)";
                return;
            }

            var report = await _driftMonitor.AnalyzeAsync(CancellationToken.None);

            KsStatistic = report.KsStatistic;
            PValue = report.PValue;
            LastAnalyzedAt = report.Timestamp.ToString("MM/dd/yyyy HH:mm:ss");
            
            DriftStatus = report.Severity switch
            {
                DriftSeverity.None => "Stable",
                DriftSeverity.Minor => "Warning",
                DriftSeverity.Moderate => "Warning", 
                DriftSeverity.Significant => "Retraining Recommended",
                _ => "Unknown"
            };

            IsRetrainingRecommended = report.Severity == DriftSeverity.Significant;
            
            StatusMessage = $"Analysis complete - Status: {DriftStatus}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetBaselineAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Setting baseline...";

            var scores = await _repository.GetAnomalyScoresAsync(200);
            if (scores.Count < 50)
            {
                StatusMessage = "Error: Insufficient data for baseline (need at least 50 scores)";
                return;
            }

            _driftMonitor.SetBaseline(scores);
            HasBaseline = true;
            UpdateBaselineInfo();
            StatusMessage = $"Baseline set with {scores.Count} samples";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Baseline error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadScoreDataAsync()
    {
        try
        {
            StatusMessage = "Loading score data...";

            var scores = await _repository.GetAnomalyScoresAsync(100);
            
            if (scores.Count > 0)
            {
                var points = scores.Select((score, index) => new ObservablePoint(index + 1, score)).ToArray();
                
                var baselineMean = HasBaseline ? CalculateBaselineMean(scores) : 0.5;
                
                ScoreSeries = new ISeries[]
                {
                    new LineSeries<ObservablePoint>
                    {
                        Values = points,
                        Name = "Anomaly Scores",
                        LineSmoothness = 0,
                        GeometrySize = 4,
                        Stroke = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 2 },
                        GeometryStroke = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 2 },
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        Fill = null
                    },
                    new LineSeries<ObservablePoint>
                    {
                        Values = new ObservablePoint[]
                        {
                            new(1, baselineMean),
                            new(points.Length, baselineMean)
                        },
                        Name = "Baseline Mean",
                        LineSmoothness = 0,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.Orange) 
                        { 
                            StrokeThickness = 1
                        },
                        Fill = null
                    }
                };

                StatusMessage = $"Loaded {scores.Count} score points";
            }
            else
            {
                StatusMessage = "No score data available";
                ScoreSeries = Array.Empty<ISeries>();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
    }

    private void UpdateBaselineInfo()
    {
        if (HasBaseline)
        {
            BaselineInfo = $"Baseline: Set on {DateTime.Now:MM/dd/yyyy}";
        }
        else
        {
            BaselineInfo = "No baseline set";
        }
    }

    private double CalculateBaselineMean(List<double> recentScores)
    {
        return recentScores.Count > 0 ? recentScores.Average() : 0.5;
    }

    public string GetStatusColor()
    {
        return DriftStatus switch
        {
            "Stable" => "#A6E3A1",          // GreenOK
            "Warning" => "#F9E2AF",         // YellowWarn  
            "Retraining Recommended" => "#F38BA8", // RedNG
            _ => "#313244"                  // Overlay
        };
    }

    public string GetRetrainingNotebookPath()
    {
        return "~/pcb-wafer-inspection-pipeline/01_training/PCB_Wafer_Training.ipynb";
    }

    public string GetRetrainingInstructions()
    {
        return "Score distribution has shifted significantly. " +
               "Open the Colab notebook to retrain the anomaly detection model with recent data. " +
               "This will help maintain detection accuracy.";
    }
}