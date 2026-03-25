using System;
using System.IO;

namespace InspectionPipeline.Core.Models;

/// <summary>
/// Application settings model for the PCB/Wafer inspection pipeline.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Base URL for the FastAPI inspection server.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:8502";

    /// <summary>
    /// Directory path to watch for new image files.
    /// </summary>
    public string WatchFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Threshold for anomaly detection (0.0 to 1.0).
    /// </summary>
    public double AnomalyThreshold { get; set; } = 0.5;

    /// <summary>
    /// Confidence threshold for YOLO segmentation (0.0 to 1.0).
    /// </summary>
    public double YoloConfThreshold { get; set; } = 0.25;

    /// <summary>
    /// Automatically save NG (no good) images when detected.
    /// </summary>
    public bool AutoSaveNg { get; set; } = true;

    /// <summary>
    /// Directory path to save NG images when auto-save is enabled.
    /// </summary>
    public string NgSavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
        "ng_images");

    /// <summary>
    /// Directory path containing PatchCore model files.
    /// </summary>
    public string PatchcoreModelDir { get; set; } = "models/patchcore/transistor/";

    /// <summary>
    /// File path to the YOLO ONNX model.
    /// </summary>
    public string YoloOnnxPath { get; set; } = "models/pcb_seg/best.onnx";
}