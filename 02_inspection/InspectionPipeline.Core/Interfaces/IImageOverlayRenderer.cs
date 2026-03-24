using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Service for rendering detection results and anomaly heatmaps on images using OpenCvSharp4.
/// </summary>
public interface IImageOverlayRenderer
{
    /// <summary>
    /// Renders segmentation detection results as colored bounding boxes and masks on the image.
    /// </summary>
    /// <param name="imageBytes">Input image as PNG/JPEG encoded bytes.</param>
    /// <param name="result">Pipeline result containing segmentation detections.</param>
    /// <returns>Rendered image as PNG encoded bytes.</returns>
    /// <remarks>
    /// Draws colored bounding boxes per defect class, semi-transparent mask overlay if available,
    /// and class label + confidence text above each bounding box.
    /// </remarks>
    byte[] RenderSegmentation(byte[] imageBytes, PipelineResult result);

    /// <summary>
    /// Renders anomaly heatmap overlay on the image using COLORMAP_JET.
    /// </summary>
    /// <param name="imageBytes">Input image as PNG/JPEG encoded bytes.</param>
    /// <param name="anomalyMap">Anomaly scores as normalized float array (0.0-1.0).</param>
    /// <param name="mapWidth">Width of the anomaly map (default: 28).</param>
    /// <param name="mapHeight">Height of the anomaly map (default: 28).</param>
    /// <param name="opacity">Heatmap blend opacity (default: 0.4f).</param>
    /// <returns>Rendered image with heatmap overlay as PNG encoded bytes.</returns>
    /// <remarks>
    /// Resizes anomaly map to original image size using bilinear interpolation,
    /// applies COLORMAP_JET (blue=normal, red=anomalous), and blends with original image.
    /// </remarks>
    byte[] RenderAnomalyHeatmap(byte[] imageBytes, float[] anomalyMap, int mapWidth = 28, int mapHeight = 28, float opacity = 0.4f);

    /// <summary>
    /// Renders combined overlay with both segmentation detections and anomaly heatmap.
    /// </summary>
    /// <param name="imageBytes">Input image as PNG/JPEG encoded bytes.</param>
    /// <param name="result">Pipeline result containing both segmentation and anomaly data.</param>
    /// <returns>Rendered image with combined overlays as PNG encoded bytes.</returns>
    /// <remarks>
    /// If result contains detections, renders segmentation overlay first.
    /// If result contains anomaly map, blends heatmap on top.
    /// </remarks>
    byte[] RenderCombined(byte[] imageBytes, PipelineResult result);
}