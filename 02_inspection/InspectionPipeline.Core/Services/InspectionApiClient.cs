using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Exceptions;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

/// <summary>
/// HTTP client for communicating with the FastAPI inspection server.
/// Uses constructor injection of HttpClient to avoid socket exhaustion.
/// </summary>
public class InspectionApiClient : IInspectionApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the InspectionApiClient class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance (injected).</param>
    /// <param name="baseUrl">The base URL of the FastAPI server.</param>
    public InspectionApiClient(HttpClient httpClient, string baseUrl = "http://localhost:8502")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        
        // Configure HttpClient timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Configure JSON serialization options for snake_case
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Sends an image to the FastAPI server for inspection and returns the analysis results.
    /// </summary>
    /// <param name="imagePath">The full file path of the image to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The inspection results from the pipeline.</returns>
    public async Task<PipelineResult> InspectAsync(string imagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));

        if (!File.Exists(imagePath))
            throw new ArgumentException($"Image file does not exist: {imagePath}", nameof(imagePath));

        var request = new InspectRequest
        {
            ImagePath = imagePath,
            Mode = "pipeline"
        };

        try
        {
            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/inspect", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                throw new InspectionApiException(
                    $"API request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<InspectResponse>(responseContent, _jsonOptions)
                ?? throw new InspectionApiException("Failed to deserialize API response");

            return MapToPipelineResult(apiResponse, imagePath);
        }
        catch (HttpRequestException ex)
        {
            throw new InspectionApiException($"HTTP request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InspectionApiException("Request timed out after 30 seconds", ex);
        }
        catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
        {
            throw; // Re-throw cancellation as-is
        }
        catch (JsonException ex)
        {
            throw new InspectionApiException($"Failed to parse JSON response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if the FastAPI server is healthy and responsive.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if server is healthy, false otherwise.</returns>
    public async Task<bool> IsServerHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health", ct);
            
            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync(ct);
            var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, _jsonOptions);
            
            return healthResponse?.Status?.Equals("ok", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            // Return false for any exception (network issues, timeouts, etc.)
            return false;
        }
    }

    /// <summary>
    /// Maps the FastAPI response to the domain PipelineResult model.
    /// </summary>
    /// <param name="apiResponse">The API response DTO.</param>
    /// <param name="imagePath">The original image path.</param>
    /// <returns>The mapped PipelineResult.</returns>
    private PipelineResult MapToPipelineResult(InspectResponse apiResponse, string imagePath)
    {
        var anomalyResult = new AnomalyResult
        {
            IsAnomalous = apiResponse.FinalResult != "OK",
            Score = apiResponse.AnomalyScore,
            Threshold = 0.5, // Default threshold, could be made configurable
            Confidence = apiResponse.AnomalyScore,
            InferenceTimeMs = apiResponse.InferenceTimeMs,
            DetectionType = "PatchCore"
        };

        SegmentationResult? segmentationResult = null;
        if (apiResponse.Detections?.Count > 0)
        {
            var detections = new List<DefectDetection>();
            foreach (var detection in apiResponse.Detections)
            {
                detections.Add(new DefectDetection
                {
                    ClassName = detection.ClassName,
                    Confidence = detection.Confidence,
                    BoundingBox = new BoundingBox
                    {
                        X = detection.BboxX,
                        Y = detection.BboxY,
                        Width = detection.BboxW,
                        Height = detection.BboxH
                    },
                    Mask = new SegmentationMask
                    {
                        Area = detection.MaskArea ?? 0
                    }
                });
            }

            segmentationResult = new SegmentationResult
            {
                HasDefects = detections.Count > 0,
                Detections = detections,
                OverallConfidence = detections.Count > 0 ? detections.Average(d => d.Confidence) : 0.0,
                InferenceTimeMs = apiResponse.InferenceTimeMs,
                ModelVersion = "YOLOv8-seg" // Could be made configurable
            };
        }

        return new PipelineResult
        {
            FinalDecision = apiResponse.FinalResult,
            DecisionStage = apiResponse.StageUsed,
            AnomalyResult = anomalyResult,
            SegmentationResult = segmentationResult,
            TotalTimeMs = apiResponse.InferenceTimeMs,
            Timestamp = DateTime.UtcNow,
            ImagePath = imagePath,
            ModelVersion = "Pipeline-v1.0" // Could be made configurable
        };
    }
}

#region DTO Classes

/// <summary>
/// Request DTO for the /inspect endpoint.
/// </summary>
internal class InspectRequest
{
    [JsonPropertyName("image_path")]
    public required string ImagePath { get; set; }

    [JsonPropertyName("mode")]
    public required string Mode { get; set; }
}

/// <summary>
/// Response DTO from the /inspect endpoint.
/// </summary>
internal class InspectResponse
{
    [JsonPropertyName("final_result")]
    public required string FinalResult { get; set; }

    [JsonPropertyName("stage_used")]
    public required string StageUsed { get; set; }

    [JsonPropertyName("anomaly_score")]
    public required double AnomalyScore { get; set; }

    [JsonPropertyName("inference_time_ms")]
    public required double InferenceTimeMs { get; set; }

    [JsonPropertyName("detections")]
    public List<DetectionDto>? Detections { get; set; }
}

/// <summary>
/// DTO for detection results from the segmentation stage.
/// </summary>
internal class DetectionDto
{
    [JsonPropertyName("class_name")]
    public required string ClassName { get; set; }

    [JsonPropertyName("confidence")]
    public required double Confidence { get; set; }

    [JsonPropertyName("bbox_x")]
    public required double BboxX { get; set; }

    [JsonPropertyName("bbox_y")]
    public required double BboxY { get; set; }

    [JsonPropertyName("bbox_w")]
    public required double BboxW { get; set; }

    [JsonPropertyName("bbox_h")]
    public required double BboxH { get; set; }

    [JsonPropertyName("mask_area")]
    public int? MaskArea { get; set; }
}

/// <summary>
/// Response DTO from the /health endpoint.
/// </summary>
internal class HealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

#endregion