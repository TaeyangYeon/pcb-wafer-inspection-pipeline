using System;
using System.Collections.Generic;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Statistical summary for dashboard display and session monitoring.
/// </summary>
public class SessionStats
{
    /// <summary>
    /// Start time of the session period.
    /// </summary>
    public required DateTime SessionStart { get; set; }

    /// <summary>
    /// End time of the session period.
    /// </summary>
    public required DateTime SessionEnd { get; set; }

    /// <summary>
    /// Total number of inspections performed during the session.
    /// </summary>
    public required int TotalInspections { get; set; }

    /// <summary>
    /// Number of inspections with "OK" result.
    /// </summary>
    public required int OkCount { get; set; }

    /// <summary>
    /// Number of inspections with "ANOMALY" result.
    /// </summary>
    public required int AnomalyCount { get; set; }

    /// <summary>
    /// Number of inspections with "NG" result (defects found).
    /// </summary>
    public required int NgCount { get; set; }

    /// <summary>
    /// Pass rate as a percentage (OK / Total * 100).
    /// </summary>
    public double PassRate => TotalInspections > 0 ? (double)OkCount / TotalInspections * 100.0 : 0.0;

    /// <summary>
    /// Anomaly rate as a percentage (Anomaly / Total * 100).
    /// </summary>
    public double AnomalyRate => TotalInspections > 0 ? (double)AnomalyCount / TotalInspections * 100.0 : 0.0;

    /// <summary>
    /// Defect rate as a percentage (NG / Total * 100).
    /// </summary>
    public double DefectRate => TotalInspections > 0 ? (double)NgCount / TotalInspections * 100.0 : 0.0;

    /// <summary>
    /// Average inference time per inspection in milliseconds.
    /// </summary>
    public required double AverageInferenceTime { get; set; }

    /// <summary>
    /// Minimum inference time recorded during the session.
    /// </summary>
    public required double MinInferenceTime { get; set; }

    /// <summary>
    /// Maximum inference time recorded during the session.
    /// </summary>
    public required double MaxInferenceTime { get; set; }

    /// <summary>
    /// Average anomaly score across all inspections.
    /// </summary>
    public required double AverageAnomalyScore { get; set; }

    /// <summary>
    /// Statistics broken down by defect class.
    /// </summary>
    public List<DefectClassStats> DefectClassBreakdown { get; set; } = new List<DefectClassStats>();

    /// <summary>
    /// Current drift analysis status.
    /// </summary>
    public DriftStatus? DriftStatus { get; set; }

    /// <summary>
    /// Processing throughput (inspections per hour).
    /// </summary>
    public double Throughput => CalculateThroughput();

    /// <summary>
    /// System uptime during the session.
    /// </summary>
    public required TimeSpan Uptime { get; set; }

    private double CalculateThroughput()
    {
        var sessionDuration = SessionEnd - SessionStart;
        return sessionDuration.TotalHours > 0 ? TotalInspections / sessionDuration.TotalHours : 0.0;
    }
}

/// <summary>
/// Statistics for a specific defect class.
/// </summary>
public class DefectClassStats
{
    /// <summary>
    /// Name of the defect class.
    /// </summary>
    public required string ClassName { get; set; }

    /// <summary>
    /// Number of detections for this class.
    /// </summary>
    public required int Count { get; set; }

    /// <summary>
    /// Average confidence score for this class.
    /// </summary>
    public required double AverageConfidence { get; set; }

    /// <summary>
    /// Percentage of total defects this class represents.
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Current drift monitoring status.
/// </summary>
public class DriftStatus
{
    /// <summary>
    /// Whether drift monitoring is active.
    /// </summary>
    public required bool IsActive { get; set; }

    /// <summary>
    /// Last drift analysis timestamp.
    /// </summary>
    public DateTime? LastAnalysis { get; set; }

    /// <summary>
    /// Current drift severity level.
    /// </summary>
    public DriftSeverity? CurrentSeverity { get; set; }

    /// <summary>
    /// Last recorded p-value from KS test.
    /// </summary>
    public double? LastPValue { get; set; }

    /// <summary>
    /// Whether baseline has been established for drift detection.
    /// </summary>
    public required bool HasBaseline { get; set; }
}