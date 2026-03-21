using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Entity representing a single defect detected during YOLOv8 segmentation.
/// </summary>
public class DefectDetail
{
    /// <summary>
    /// Primary key for the defect detail record.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent inspection record.
    /// </summary>
    [ForeignKey(nameof(InspectionRecord))]
    public int InspectionRecordId { get; set; }

    /// <summary>
    /// Class name of the detected defect (e.g., "missing_hole", "mouse_bite").
    /// </summary>
    [Required]
    public required string ClassName { get; set; }

    /// <summary>
    /// Confidence score of the detection (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding box X coordinate (top-left corner).
    /// </summary>
    public double BboxX { get; set; }

    /// <summary>
    /// Bounding box Y coordinate (top-left corner).
    /// </summary>
    public double BboxY { get; set; }

    /// <summary>
    /// Bounding box width.
    /// </summary>
    public double BboxW { get; set; }

    /// <summary>
    /// Bounding box height.
    /// </summary>
    public double BboxH { get; set; }

    /// <summary>
    /// Segmentation mask area in pixels.
    /// </summary>
    public int MaskArea { get; set; }

    /// <summary>
    /// Navigation property back to the parent inspection record.
    /// </summary>
    public InspectionRecord InspectionRecord { get; set; } = null!;
}