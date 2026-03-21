using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Service for monitoring drift in anomaly scores using statistical tests.
/// </summary>
public interface IDriftMonitor
{
    /// <summary>
    /// Analyzes the current anomaly score distribution against the baseline using statistical tests.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a drift analysis report.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no baseline has been set or insufficient data is available for analysis.</exception>
    /// <remarks>
    /// Uses the Kolmogorov-Smirnov test to compare current score distribution with the baseline.
    /// Requires at least 30 samples in both baseline and current data for reliable analysis.
    /// </remarks>
    Task<DriftReport> AnalyzeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the baseline anomaly score distribution for drift detection.
    /// </summary>
    /// <param name="baselineScores">A collection of anomaly scores representing normal operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when baselineScores is null.</exception>
    /// <exception cref="ArgumentException">Thrown when baselineScores is empty or contains fewer than 30 samples.</exception>
    /// <remarks>
    /// The baseline should be established during a period of known good/normal operation.
    /// Larger baseline datasets (100+ samples) provide more reliable drift detection.
    /// </remarks>
    void SetBaseline(List<double> baselineScores);

    /// <summary>
    /// Gets a value indicating whether a baseline has been set for drift analysis.
    /// </summary>
    /// <remarks>
    /// Must be true before calling AnalyzeAsync. The baseline is typically set during system calibration
    /// or after confirming a period of normal operation.
    /// </remarks>
    bool HasBaseline { get; }
}