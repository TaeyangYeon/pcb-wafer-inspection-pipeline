using System;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Service for managing threshold-based alarms triggered by drift detection and inspection failures.
/// </summary>
public interface IAlarmService
{
    /// <summary>
    /// Event triggered when an alarm condition is detected.
    /// </summary>
    /// <remarks>
    /// The AlarmEvent parameter contains details about the alarm including severity, message, and timestamp.
    /// Subscribers should handle this event asynchronously to avoid blocking the alarm detection process.
    /// </remarks>
    event EventHandler<AlarmEvent> AlarmTriggered;

    /// <summary>
    /// Evaluates the drift report against configured thresholds and triggers alarms if necessary.
    /// </summary>
    /// <param name="report">The drift analysis report to evaluate.</param>
    /// <exception cref="ArgumentNullException">Thrown when the report is null.</exception>
    /// <remarks>
    /// This method checks the drift report p-values and other metrics against configured thresholds.
    /// Alarms are triggered for significant drift (p-value &lt; 0.05), moderate drift (p-value &lt; 0.1),
    /// or high anomaly scores that exceed threshold limits.
    /// </remarks>
    void CheckAndTrigger(DriftReport report);

    /// <summary>
    /// Gets a value indicating whether the alarm service is currently in an alarming state.
    /// </summary>
    /// <remarks>
    /// Returns true if any alarm condition is currently active. The state persists until Reset() is called
    /// or the underlying condition is resolved.
    /// </remarks>
    bool IsAlarming { get; }

    /// <summary>
    /// Resets the alarm service, clearing all active alarm conditions.
    /// </summary>
    /// <remarks>
    /// This method acknowledges all current alarms and returns the service to a non-alarming state.
    /// Should typically be called by an operator after investigating and addressing alarm conditions.
    /// </remarks>
    void Reset();
}