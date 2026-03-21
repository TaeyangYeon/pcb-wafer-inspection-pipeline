using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Entity representing a single inspection record in the database.
/// </summary>
public class InspectionRecord
{
    /// <summary>
    /// Primary key for the inspection record.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Timestamp when the inspection was performed.
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// File path of the inspected image.
    /// </summary>
    [Required]
    public required string ImagePath { get; set; }

    /// <summary>
    /// Final inspection result: "OK", "ANOMALY", or "NG".
    /// </summary>
    [Required]
    public required string FinalResult { get; set; }

    /// <summary>
    /// The stage that was used for the final decision: "Anomaly", "Segment", or "Pipeline".
    /// </summary>
    [Required]
    public required string StageUsed { get; set; }

    /// <summary>
    /// Anomaly score from PatchCore analysis (0.0 to 1.0).
    /// </summary>
    public double AnomalyScore { get; set; }

    /// <summary>
    /// Total inference time in milliseconds.
    /// </summary>
    public double InferenceTimeMs { get; set; }

    /// <summary>
    /// Foreign key to the ModelVersion table.
    /// </summary>
    [ForeignKey(nameof(ModelVersion))]
    public int ModelVersionId { get; set; }

    /// <summary>
    /// Navigation property to the associated model version.
    /// </summary>
    public ModelVersion ModelVersion { get; set; } = null!;

    /// <summary>
    /// Navigation property to defect details found during segmentation.
    /// </summary>
    public List<DefectDetail> DefectDetails { get; set; } = new List<DefectDetail>();
}