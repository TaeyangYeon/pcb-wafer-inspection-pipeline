using System;
using System.Collections.Generic;
using System.Globalization;
using OpenCvSharp;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

public class ImageOverlayRenderer : IImageOverlayRenderer
{
    private static readonly Dictionary<string, Scalar> DefectColors = new()
    {
        { "missing_hole", new Scalar(0, 0, 255) },      // Red (BGR)
        { "mouse_bite", new Scalar(255, 0, 0) },        // Blue (BGR)
        { "open_circuit", new Scalar(0, 255, 0) },      // Green (BGR)
        { "short", new Scalar(0, 255, 255) },           // Yellow (BGR)
        { "spur", new Scalar(255, 0, 255) },            // Magenta (BGR)
        { "spurious_copper", new Scalar(255, 255, 0) }  // Cyan (BGR)
    };

    private const HersheyFonts FontFace = HersheyFonts.HersheySimplex;
    private const double FontScale = 0.6;
    private const int FontThickness = 2;
    private const int BboxThickness = 2;

    public byte[] RenderSegmentation(byte[] imageBytes, PipelineResult result)
    {
        if (imageBytes == null)
            throw new ArgumentNullException(nameof(imageBytes));
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        using var image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (image.Empty())
            throw new ArgumentException("Failed to decode image bytes", nameof(imageBytes));

        if (result.SegmentationResult?.HasDefects == true)
        {
            foreach (var detection in result.SegmentationResult.Detections)
            {
                DrawDetection(image, detection);
            }
        }

        return EncodeImageToPng(image);
    }

    public byte[] RenderAnomalyHeatmap(byte[] imageBytes, float[] anomalyMap, int mapWidth = 28, int mapHeight = 28, float opacity = 0.4f)
    {
        if (imageBytes == null)
            throw new ArgumentNullException(nameof(imageBytes));
        if (anomalyMap == null)
            throw new ArgumentNullException(nameof(anomalyMap));
        if (anomalyMap.Length != mapWidth * mapHeight)
            throw new ArgumentException($"Anomaly map size mismatch. Expected {mapWidth * mapHeight}, got {anomalyMap.Length}");

        using var originalImage = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (originalImage.Empty())
            throw new ArgumentException("Failed to decode image bytes", nameof(imageBytes));

        using var heatmapMat = CreateHeatmapMat(anomalyMap, mapWidth, mapHeight);
        using var resizedHeatmap = new Mat();
        
        Cv2.Resize(heatmapMat, resizedHeatmap, originalImage.Size(), interpolation: InterpolationFlags.Linear);
        
        using var coloredHeatmap = new Mat();
        Cv2.ApplyColorMap(resizedHeatmap, coloredHeatmap, ColormapTypes.Jet);

        using var blendedImage = new Mat();
        Cv2.AddWeighted(originalImage, 1.0 - opacity, coloredHeatmap, opacity, 0, blendedImage);

        return EncodeImageToPng(blendedImage);
    }

    public byte[] RenderCombined(byte[] imageBytes, PipelineResult result)
    {
        if (imageBytes == null)
            throw new ArgumentNullException(nameof(imageBytes));
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        using var image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (image.Empty())
            throw new ArgumentException("Failed to decode image bytes", nameof(imageBytes));

        Mat finalImage = image;

        if (result.AnomalyResult?.AnomalyMap != null)
        {
            using var heatmapMat = CreateHeatmapMat(result.AnomalyResult.AnomalyMap, 28, 28);
            using var resizedHeatmap = new Mat();
            
            Cv2.Resize(heatmapMat, resizedHeatmap, image.Size(), interpolation: InterpolationFlags.Linear);
            
            using var coloredHeatmap = new Mat();
            Cv2.ApplyColorMap(resizedHeatmap, coloredHeatmap, ColormapTypes.Jet);

            using var blendedImage = new Mat();
            Cv2.AddWeighted(image, 0.6, coloredHeatmap, 0.4, 0, blendedImage);
            
            finalImage = blendedImage.Clone();
        }

        if (result.SegmentationResult?.HasDefects == true)
        {
            foreach (var detection in result.SegmentationResult.Detections)
            {
                DrawDetection(finalImage, detection);
            }
        }

        var resultBytes = EncodeImageToPng(finalImage);
        
        if (finalImage != image)
        {
            finalImage.Dispose();
        }

        return resultBytes;
    }

    private void DrawDetection(Mat image, DefectDetection detection)
    {
        var color = GetDefectColor(detection.ClassName);
        
        var rect = new Rect(
            (int)detection.BoundingBox.X,
            (int)detection.BoundingBox.Y,
            (int)detection.BoundingBox.Width,
            (int)detection.BoundingBox.Height
        );

        Cv2.Rectangle(image, rect, color, BboxThickness);

        if (detection.Mask?.Polygon != null && detection.Mask.Polygon.Count > 0)
        {
            var points = detection.Mask.Polygon.ConvertAll(p => new OpenCvSharp.Point((int)p.X, (int)p.Y));
            var pointsArray = points.ToArray();
            
            using var mask = Mat.Zeros(image.Size(), MatType.CV_8UC1);
            Cv2.FillPoly(mask, new[] { pointsArray }, Scalar.White);
            
            using var coloredMask = new Mat();
            Cv2.CvtColor(mask, coloredMask, ColorConversionCodes.GRAY2BGR);
            
            using var maskOverlay = new Mat();
            coloredMask.SetTo(color, mask);
            
            Cv2.AddWeighted(image, 0.7, maskOverlay, 0.3, 0, image);
        }

        var label = $"{detection.ClassName} {detection.Confidence:F2}";
        var labelSize = Cv2.GetTextSize(label, FontFace, FontScale, FontThickness, out var baseline);
        
        var labelRect = new Rect(
            rect.X,
            rect.Y - labelSize.Height - baseline - 5,
            labelSize.Width + 10,
            labelSize.Height + baseline + 10
        );

        if (labelRect.Y < 0)
        {
            labelRect.Y = rect.Bottom;
        }

        Cv2.Rectangle(image, labelRect, color, Cv2.FILLED);
        
        Cv2.PutText(
            image,
            label,
            new OpenCvSharp.Point(labelRect.X + 5, labelRect.Y + labelSize.Height + 5),
            FontFace,
            FontScale,
            Scalar.White,
            FontThickness
        );
    }

    private Mat CreateHeatmapMat(float[] anomalyMap, int width, int height)
    {
        var data = new byte[anomalyMap.Length];
        
        for (int i = 0; i < anomalyMap.Length; i++)
        {
            var normalizedValue = Math.Max(0.0f, Math.Min(1.0f, anomalyMap[i]));
            data[i] = (byte)(normalizedValue * 255);
        }
        
        var mat = Mat.FromPixelData(height, width, MatType.CV_8UC1, data);
        return mat;
    }

    private Scalar GetDefectColor(string className)
    {
        return DefectColors.TryGetValue(className.ToLowerInvariant(), out var color) 
            ? color 
            : new Scalar(128, 128, 128); // Default gray color
    }

    private byte[] EncodeImageToPng(Mat image)
    {
        var success = Cv2.ImEncode(".png", image, out var encodedImage);
        if (!success)
            throw new InvalidOperationException("Failed to encode image to PNG");
        
        return encodedImage;
    }
}