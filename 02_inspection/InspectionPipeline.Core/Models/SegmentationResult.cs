using System.Collections.Generic;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Result from YOLOv8-seg defect segmentation analysis.
/// </summary>
public class SegmentationResult
{
    /// <summary>
    /// Indicates whether any defects were detected.
    /// </summary>
    public required bool HasDefects { get; set; }

    /// <summary>
    /// List of detected defects with their details.
    /// </summary>
    public required List<DefectDetection> Detections { get; set; }

    /// <summary>
    /// Overall confidence score for the segmentation result.
    /// </summary>
    public required double OverallConfidence { get; set; }

    /// <summary>
    /// Time taken for segmentation inference in milliseconds.
    /// </summary>
    public required double InferenceTimeMs { get; set; }

    /// <summary>
    /// Path to the annotated output image, if generated.
    /// </summary>
    public string? AnnotatedImagePath { get; set; }

    /// <summary>
    /// Model version used for this segmentation.
    /// </summary>
    public required string ModelVersion { get; set; }
}

/// <summary>
/// Represents a single defect detection from YOLOv8-seg.
/// </summary>
public class DefectDetection
{
    /// <summary>
    /// Class name of the detected defect.
    /// </summary>
    public required string ClassName { get; set; }

    /// <summary>
    /// Confidence score of the detection (0.0 to 1.0).
    /// </summary>
    public required double Confidence { get; set; }

    /// <summary>
    /// Bounding box coordinates and dimensions.
    /// </summary>
    public required BoundingBox BoundingBox { get; set; }

    /// <summary>
    /// Segmentation mask information.
    /// </summary>
    public required SegmentationMask Mask { get; set; }
}

/// <summary>
/// Represents a bounding box for object detection.
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// X coordinate of the top-left corner.
    /// </summary>
    public required double X { get; set; }

    /// <summary>
    /// Y coordinate of the top-left corner.
    /// </summary>
    public required double Y { get; set; }

    /// <summary>
    /// Width of the bounding box.
    /// </summary>
    public required double Width { get; set; }

    /// <summary>
    /// Height of the bounding box.
    /// </summary>
    public required double Height { get; set; }
}

/// <summary>
/// Represents segmentation mask information.
/// </summary>
public class SegmentationMask
{
    /// <summary>
    /// Area of the segmentation mask in pixels.
    /// </summary>
    public required int Area { get; set; }

    /// <summary>
    /// Polygon points defining the mask boundary (optional).
    /// </summary>
    public List<Point>? Polygon { get; set; }
}

/// <summary>
/// Represents a 2D point.
/// </summary>
public class Point
{
    /// <summary>
    /// X coordinate.
    /// </summary>
    public required double X { get; set; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public required double Y { get; set; }
}