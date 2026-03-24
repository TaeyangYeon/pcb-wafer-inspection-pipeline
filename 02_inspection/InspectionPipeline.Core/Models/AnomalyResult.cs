namespace InspectionPipeline.Core.Models;

/// <summary>
/// Result from PatchCore anomaly detection analysis.
/// </summary>
public class AnomalyResult
{
    /// <summary>
    /// Indicates whether an anomaly was detected.
    /// </summary>
    public required bool IsAnomalous { get; set; }

    /// <summary>
    /// Anomaly score between 0.0 (normal) and 1.0 (highly anomalous).
    /// </summary>
    public required double Score { get; set; }

    /// <summary>
    /// Threshold value used for anomaly classification.
    /// </summary>
    public required double Threshold { get; set; }

    /// <summary>
    /// Confidence in the anomaly detection result.
    /// </summary>
    public required double Confidence { get; set; }

    /// <summary>
    /// Path to the anomaly heatmap image, if generated.
    /// </summary>
    public string? HeatmapPath { get; set; }

    /// <summary>
    /// Raw anomaly map as float array (28x28 = 784 values, normalized 0.0-1.0).
    /// </summary>
    public float[]? AnomalyMap { get; set; }

    /// <summary>
    /// Time taken for anomaly detection in milliseconds.
    /// </summary>
    public required double InferenceTimeMs { get; set; }

    /// <summary>
    /// Type of anomaly detection performed (e.g., "transistor", "grid").
    /// </summary>
    public required string DetectionType { get; set; }
}