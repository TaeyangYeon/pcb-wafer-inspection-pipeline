using System;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Combined result from the 2-stage inspection pipeline (PatchCore + YOLOv8-seg).
/// </summary>
public class PipelineResult
{
    /// <summary>
    /// Final inspection decision: "OK", "ANOMALY", or "NG".
    /// </summary>
    public required string FinalDecision { get; set; }

    /// <summary>
    /// The stage that made the final decision: "Anomaly", "Segment", or "Pipeline".
    /// </summary>
    public required string DecisionStage { get; set; }

    /// <summary>
    /// Result from anomaly detection stage (always performed).
    /// </summary>
    public required AnomalyResult AnomalyResult { get; set; }

    /// <summary>
    /// Result from segmentation stage (performed only if anomaly detected).
    /// </summary>
    public SegmentationResult? SegmentationResult { get; set; }

    /// <summary>
    /// Total pipeline execution time in milliseconds.
    /// </summary>
    public required double TotalTimeMs { get; set; }

    /// <summary>
    /// Timestamp when the inspection was performed.
    /// </summary>
    public required DateTime Timestamp { get; set; }

    /// <summary>
    /// Path to the original image that was inspected.
    /// </summary>
    public required string ImagePath { get; set; }

    /// <summary>
    /// Version of the model used for this inspection.
    /// </summary>
    public required string ModelVersion { get; set; }

    /// <summary>
    /// Additional metadata about the inspection process.
    /// </summary>
    public InspectionMetadata? Metadata { get; set; }
}

/// <summary>
/// Additional metadata about the inspection process.
/// </summary>
public class InspectionMetadata
{
    /// <summary>
    /// Original image dimensions.
    /// </summary>
    public ImageDimensions? ImageSize { get; set; }

    /// <summary>
    /// Processing configuration used.
    /// </summary>
    public string? ProcessingConfig { get; set; }

    /// <summary>
    /// Quality score of the input image (if calculated).
    /// </summary>
    public double? ImageQuality { get; set; }

    /// <summary>
    /// Any warnings or notes from the inspection process.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Represents image dimensions.
/// </summary>
public class ImageDimensions
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public required int Width { get; set; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public required int Height { get; set; }

    /// <summary>
    /// Number of color channels.
    /// </summary>
    public required int Channels { get; set; }
}