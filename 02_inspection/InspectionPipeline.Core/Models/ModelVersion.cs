using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Entity representing a model version loaded in the system.
/// </summary>
public class ModelVersion
{
    /// <summary>
    /// Primary key for the model version record.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Name of the model (e.g., "YOLOv8-seg", "PatchCore-Transistor").
    /// </summary>
    [Required]
    public required string ModelName { get; set; }

    /// <summary>
    /// Version identifier of the model.
    /// </summary>
    [Required]
    public required string Version { get; set; }

    /// <summary>
    /// Timestamp when the model was loaded into the system.
    /// </summary>
    [Required]
    public DateTime LoadedAt { get; set; }

    /// <summary>
    /// File path where the model is stored.
    /// </summary>
    [Required]
    public required string FilePath { get; set; }

    /// <summary>
    /// Navigation property to all inspection records that used this model version.
    /// </summary>
    public List<InspectionRecord> InspectionRecords { get; set; } = new List<InspectionRecord>();
}