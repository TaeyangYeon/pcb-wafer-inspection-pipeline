using System;
using System.Collections.Generic;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Report from drift analysis using Kolmogorov-Smirnov test on anomaly scores.
/// </summary>
public class DriftReport
{
    /// <summary>
    /// Timestamp when the drift analysis was performed.
    /// </summary>
    public required DateTime Timestamp { get; set; }

    /// <summary>
    /// P-value from the Kolmogorov-Smirnov test (0.0 to 1.0).
    /// </summary>
    public required double PValue { get; set; }

    /// <summary>
    /// KS statistic value from the test.
    /// </summary>
    public required double KsStatistic { get; set; }

    /// <summary>
    /// Indicates whether significant drift was detected (typically p-value < 0.05).
    /// </summary>
    public required bool IsDriftDetected { get; set; }

    /// <summary>
    /// Severity level of the detected drift.
    /// </summary>
    public required DriftSeverity Severity { get; set; }

    /// <summary>
    /// Number of baseline samples used in the analysis.
    /// </summary>
    public required int BaselineSampleCount { get; set; }

    /// <summary>
    /// Number of current samples used in the analysis.
    /// </summary>
    public required int CurrentSampleCount { get; set; }

    /// <summary>
    /// Statistical summary of the baseline distribution.
    /// </summary>
    public required DistributionStats BaselineStats { get; set; }

    /// <summary>
    /// Statistical summary of the current distribution.
    /// </summary>
    public required DistributionStats CurrentStats { get; set; }

    /// <summary>
    /// Recommended actions based on the drift analysis.
    /// </summary>
    public List<string> Recommendations { get; set; } = new List<string>();

    /// <summary>
    /// Additional notes or warnings from the analysis.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Severity levels for drift detection.
/// </summary>
public enum DriftSeverity
{
    /// <summary>
    /// No drift detected (p-value >= 0.1).
    /// </summary>
    None,

    /// <summary>
    /// Minor drift detected (0.05 <= p-value < 0.1).
    /// </summary>
    Minor,

    /// <summary>
    /// Moderate drift detected (0.01 <= p-value < 0.05).
    /// </summary>
    Moderate,

    /// <summary>
    /// Significant drift detected (p-value < 0.01).
    /// </summary>
    Significant
}

/// <summary>
/// Statistical summary of a distribution.
/// </summary>
public class DistributionStats
{
    /// <summary>
    /// Mean value of the distribution.
    /// </summary>
    public required double Mean { get; set; }

    /// <summary>
    /// Standard deviation of the distribution.
    /// </summary>
    public required double StandardDeviation { get; set; }

    /// <summary>
    /// Minimum value in the distribution.
    /// </summary>
    public required double Minimum { get; set; }

    /// <summary>
    /// Maximum value in the distribution.
    /// </summary>
    public required double Maximum { get; set; }

    /// <summary>
    /// 25th percentile (Q1) of the distribution.
    /// </summary>
    public required double Q25 { get; set; }

    /// <summary>
    /// 50th percentile (median) of the distribution.
    /// </summary>
    public required double Median { get; set; }

    /// <summary>
    /// 75th percentile (Q3) of the distribution.
    /// </summary>
    public required double Q75 { get; set; }
}