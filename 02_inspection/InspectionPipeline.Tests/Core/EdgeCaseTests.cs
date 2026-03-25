using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Exceptions;
using InspectionPipeline.Core.Models;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.Tests.Core;

[TestFixture]
public class EdgeCaseTests
{
    private InspectionDbContext _context;
    private InspectionRepository _repository;
    private Mock<ILogger<FileWatcherService>> _mockLogger;
    private Mock<HttpMessageHandler> _mockHttpHandler;
    private HttpClient _httpClient;
    private InspectionApiClient _apiClient;
    private DriftMonitor _driftMonitor;
    private ImageOverlayRenderer _overlayRenderer;

    [SetUp]
    public async Task SetUp()
    {
        // Setup InMemory database
        var options = new DbContextOptionsBuilder<InspectionDbContext>()
            .UseInMemoryDatabase($"EdgeCaseTestDb_{Guid.NewGuid()}")
            .Options;
        
        _context = new InspectionDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new InspectionRepository(_context);

        // Setup mocks
        _mockLogger = new Mock<ILogger<FileWatcherService>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _apiClient = new InspectionApiClient(_httpClient);
        _driftMonitor = new DriftMonitor(_repository);
        _overlayRenderer = new ImageOverlayRenderer();
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetRecordsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var records = await _repository.GetRecordsAsync();

        // Assert
        Assert.That(records, Is.Not.Null);
        Assert.That(records.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetStatsAsync_EmptyDatabase_ReturnsAllZeros()
    {
        // Act
        var stats = await _repository.GetStatsAsync();

        // Assert
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.TotalInspections, Is.EqualTo(0));
        Assert.That(stats.OkCount, Is.EqualTo(0));
        Assert.That(stats.NgCount, Is.EqualTo(0));
        Assert.That(stats.AnomalyCount, Is.EqualTo(0));
        Assert.That(stats.DefectRate, Is.EqualTo(0.0));
        Assert.That(stats.AverageInferenceTime, Is.EqualTo(0.0));
        Assert.That(stats.AverageAnomalyScore, Is.EqualTo(0.0));
    }

    [Test]
    public async Task GetAnomalyScoresAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var scores = await _repository.GetAnomalyScoresAsync(10);

        // Assert
        Assert.That(scores, Is.Not.Null);
        Assert.That(scores.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ExportToCsvAsync_EmptyDatabase_WritesHeaderOnly()
    {
        // Arrange
        var tempCsvPath = Path.GetTempFileName();
        
        try
        {
            // Act
            await _repository.ExportToCsvAsync(tempCsvPath);

            // Assert
            Assert.That(File.Exists(tempCsvPath), Is.True);
            var lines = File.ReadAllLines(tempCsvPath);
            Assert.That(lines.Length, Is.EqualTo(1)); // Header only
            Assert.That(lines[0], Is.EqualTo("Timestamp,ImagePath,FinalResult,StageUsed,AnomalyScore,InferenceTimeMs,DefectCount"));
        }
        finally
        {
            if (File.Exists(tempCsvPath))
                File.Delete(tempCsvPath);
        }
    }

    [Test]
    public async Task InspectAsync_MalformedJsonResponse_ThrowsInspectionApiException()
    {
        // Arrange
        var tempImagePath = Path.GetTempFileName();
        File.WriteAllBytes(tempImagePath, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header

        // Mock HTTP response with malformed JSON
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{ malformed json without closing brace", Encoding.UTF8, "application/json")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        try
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<InspectionApiException>(
                async () => await _apiClient.InspectAsync(tempImagePath));
            Assert.That(exception.Message, Does.Contain("Failed to parse JSON response"));
        }
        finally
        {
            if (File.Exists(tempImagePath))
                File.Delete(tempImagePath);
        }
    }

    [Test]
    public void FileWatcherService_FolderNotExist_ThrowsArgumentException()
    {
        // Arrange
        var fileWatcher = new FileWatcherService(_mockLogger.Object);
        var nonExistentPath = "/path/that/does/not/exist";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => fileWatcher.Start(nonExistentPath));
        Assert.That(exception.Message, Does.Contain("Directory does not exist"));
    }

    [Test]
    public async Task DriftMonitor_SingleElementBaseline_ReturnsInsufficientData()
    {
        // Arrange
        var singleElementBaseline = new List<double> { 0.5 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => _driftMonitor.SetBaseline(singleElementBaseline));
        Assert.That(exception.Message, Does.Contain("Baseline requires at least 30 samples"));
    }

    [Test]
    public void ImageOverlayRenderer_EmptyDetectionList_ReturnsOriginalImage()
    {
        // Arrange
        var simpleImageBytes = CreateSimplePngImage();
        var pipelineResult = new PipelineResult
        {
            FinalDecision = "OK",
            DecisionStage = "Pipeline",
            AnomalyResult = new AnomalyResult
            {
                IsAnomalous = false,
                Score = 0.1,
                Threshold = 0.5,
                Confidence = 0.9,
                InferenceTimeMs = 80,
                DetectionType = "transistor"
            },
            SegmentationResult = new SegmentationResult
            {
                HasDefects = false,
                Detections = new List<DefectDetection>(), // Empty list
                OverallConfidence = 0.0,
                InferenceTimeMs = 50,
                ModelVersion = "YOLOv8-seg-1.0.0"
            },
            TotalTimeMs = 130,
            Timestamp = DateTime.Now,
            ImagePath = "/test/image.jpg",
            ModelVersion = "TestModel-1.0.0",
            Metadata = null
        };

        // Act
        var resultBytes = _overlayRenderer.RenderSegmentation(simpleImageBytes, pipelineResult);

        // Assert
        Assert.That(resultBytes, Is.Not.Null);
        Assert.That(resultBytes.Length, Is.GreaterThan(0));
        // Should return valid PNG bytes without crashing
        Assert.That(resultBytes[0], Is.EqualTo(0x89)); // PNG signature
        Assert.That(resultBytes[1], Is.EqualTo(0x50)); // PNG signature
    }

    [Test]
    public async Task InspectionRepository_NullImagePath_ThrowsException()
    {
        // Arrange
        var recordWithNullPath = new InspectionRecord
        {
            ImagePath = null!, // Null path
            FinalResult = "OK",
            StageUsed = "Pipeline",
            AnomalyScore = 0.1,
            InferenceTimeMs = 100,
            Timestamp = DateTime.Now,
            ModelVersionId = 1
        };

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(
            async () => await _repository.SaveRecordAsync(recordWithNullPath));
        // Should throw due to required field validation
    }

    [Test]
    public async Task InspectionRepository_EmptyImagePath_ThrowsException()
    {
        // Arrange
        var recordWithEmptyPath = new InspectionRecord
        {
            ImagePath = string.Empty, // Empty path
            FinalResult = "OK",
            StageUsed = "Pipeline",
            AnomalyScore = 0.1,
            InferenceTimeMs = 100,
            Timestamp = DateTime.Now,
            ModelVersionId = 1
        };

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(
            async () => await _repository.SaveRecordAsync(recordWithEmptyPath));
        // Should throw due to required field validation or database constraint
    }

    [Test]
    public async Task DriftMonitor_EmptyDatabase_ReturnsNoDrift()
    {
        // Arrange - Set baseline but no current data in DB
        var baselineScores = new List<double>();
        for (int i = 0; i < 60; i++)
        {
            baselineScores.Add(0.1 + (i * 0.001)); // Valid baseline with 60 samples
        }
        _driftMonitor.SetBaseline(baselineScores);

        // Act
        var driftReport = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(driftReport, Is.Not.Null);
        Assert.That(driftReport.IsDriftDetected, Is.False);
        Assert.That(driftReport.CurrentSampleCount, Is.EqualTo(0));
        Assert.That(driftReport.BaselineSampleCount, Is.EqualTo(60));
    }

    [Test]
    public void ImageOverlayRenderer_NullImageBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var pipelineResult = new PipelineResult
        {
            FinalDecision = "OK",
            DecisionStage = "Pipeline",
            AnomalyResult = new AnomalyResult
            {
                IsAnomalous = false,
                Score = 0.1,
                Threshold = 0.5,
                Confidence = 0.9,
                InferenceTimeMs = 80,
                DetectionType = "transistor"
            },
            SegmentationResult = null,
            TotalTimeMs = 130,
            Timestamp = DateTime.Now,
            ImagePath = "/test/image.jpg",
            ModelVersion = "TestModel-1.0.0",
            Metadata = null
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => _overlayRenderer.RenderSegmentation(null!, pipelineResult));
        Assert.That(exception.ParamName, Is.EqualTo("imageBytes"));
    }

    private static byte[] CreateSimplePngImage()
    {
        // Create a minimal valid PNG image (1x1 pixel)
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
            0x01, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82
        };
    }
}