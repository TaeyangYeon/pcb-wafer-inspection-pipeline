using System;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Event data for alarm triggers in the inspection system.
/// </summary>
public class AlarmEvent
{
    /// <summary>
    /// Unique identifier for the alarm event.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Timestamp when the alarm was triggered.
    /// </summary>
    public required DateTime Timestamp { get; set; }

    /// <summary>
    /// Severity level of the alarm.
    /// </summary>
    public required AlarmSeverity Severity { get; set; }

    /// <summary>
    /// Type of alarm that was triggered.
    /// </summary>
    public required AlarmType Type { get; set; }

    /// <summary>
    /// Human-readable message describing the alarm condition.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Source of the alarm (e.g., "DriftMonitor", "InspectionPipeline").
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Associated drift report that triggered the alarm (if applicable).
    /// </summary>
    public DriftReport? DriftReport { get; set; }

    /// <summary>
    /// Value that triggered the alarm threshold.
    /// </summary>
    public double? TriggerValue { get; set; }

    /// <summary>
    /// Threshold value that was exceeded.
    /// </summary>
    public double? ThresholdValue { get; set; }

    /// <summary>
    /// Recommended actions to address the alarm.
    /// </summary>
    public string? RecommendedAction { get; set; }

    /// <summary>
    /// Whether the alarm has been acknowledged by an operator.
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Timestamp when the alarm was acknowledged (if applicable).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// User who acknowledged the alarm (if applicable).
    /// </summary>
    public string? AcknowledgedBy { get; set; }
}

/// <summary>
/// Severity levels for alarms.
/// </summary>
public enum AlarmSeverity
{
    /// <summary>
    /// Informational message, no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning condition that should be monitored.
    /// </summary>
    Warning,

    /// <summary>
    /// Error condition that requires attention.
    /// </summary>
    Error,

    /// <summary>
    /// Critical condition that requires immediate action.
    /// </summary>
    Critical
}

/// <summary>
/// Types of alarms in the inspection system.
/// </summary>
public enum AlarmType
{
    /// <summary>
    /// Drift detected in anomaly scores.
    /// </summary>
    DriftDetected,

    /// <summary>
    /// High anomaly score threshold exceeded.
    /// </summary>
    HighAnomalyScore,

    /// <summary>
    /// Excessive defect detection rate.
    /// </summary>
    HighDefectRate,

    /// <summary>
    /// Model inference failure.
    /// </summary>
    InferenceFailure,

    /// <summary>
    /// System performance degradation.
    /// </summary>
    PerformanceDegradation,

    /// <summary>
    /// Model health check failure.
    /// </summary>
    ModelHealthFailure
}