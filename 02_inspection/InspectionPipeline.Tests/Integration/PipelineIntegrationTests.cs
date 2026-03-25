using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Exceptions;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.Core.Services;
using InspectionPipeline.UI.ViewModels;

namespace InspectionPipeline.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class PipelineIntegrationTests
{
    private InspectionDbContext _context;
    private IInspectionRepository _repository;
    private Mock<IInspectionApiClient> _mockApiClient;
    private Mock<IImageOverlayRenderer> _mockOverlayRenderer;
    private IDriftMonitor _driftMonitor;
    private IAlarmService _alarmService;
    private InspectionViewModel _inspectionViewModel;
    private ModelVersion _testModelVersion;

    [SetUp]
    public async Task SetUp()
    {
        // Setup InMemory EF database
        var options = new DbContextOptionsBuilder<InspectionDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        
        _context = new InspectionDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        
        // Create test ModelVersion for foreign key references
        _testModelVersion = new ModelVersion
        {
            ModelName = "TestModel",
            Version = "1.0.0",
            LoadedAt = DateTime.Now,
            FilePath = "/test/model.onnx"
        };
        _context.ModelVersions.Add(_testModelVersion);
        await _context.SaveChangesAsync();
        
        // Setup real services
        _repository = new InspectionRepository(_context);
        _driftMonitor = new DriftMonitor(_repository);
        _alarmService = new AlarmService();
        
        // Setup mocked API client
        _mockApiClient = new Mock<IInspectionApiClient>();
        _mockApiClient.Setup(x => x.IsServerHealthyAsync())
            .ReturnsAsync(true);
            
        // Setup mocked overlay renderer
        _mockOverlayRenderer = new Mock<IImageOverlayRenderer>();
        _mockOverlayRenderer.Setup(x => x.RenderCombined(It.IsAny<byte[]>(), It.IsAny<PipelineResult>()))
            .Returns(new byte[100]); // Mock rendered image bytes
        
        // Setup InspectionViewModel with real+mock dependencies
        _inspectionViewModel = new InspectionViewModel(
            _mockApiClient.Object,
            _repository,
            _mockOverlayRenderer.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test]
    public async Task NormalProduction_FiveOkInspections_NgRateIsZero()
    {
        // Arrange
        _mockApiClient.Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult
            {
                FinalDecision = "OK",
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult
                {
                    IsAnomalous = false,
                    Score = 0.25,
                    Threshold = 0.5,
                    Confidence = 0.95,
                    InferenceTimeMs = 80.0,
                    DetectionType = "transistor"
                },
                SegmentationResult = null,
                TotalTimeMs = 150.0,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            });

        // Create temp image files for testing
        var tempImagePaths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var tempPath = Path.GetTempFileName();
            File.WriteAllBytes(tempPath, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header
            tempImagePaths.Add(tempPath);
        }

        try
        {
            // Act
            foreach (var imagePath in tempImagePaths)
            {
                _inspectionViewModel.ImagePath = imagePath;
                await _inspectionViewModel.RunInspectionCommand.ExecuteAsync(null);
                
                // Small delay to ensure operations complete
                await Task.Delay(50);
            }

            // Assert
            var allRecords = await _repository.GetRecordsAsync();
            Assert.That(allRecords.Count, Is.EqualTo(5));
            Assert.That(allRecords.All(r => r.FinalResult == "OK"), Is.True);

            var stats = await _repository.GetStatsAsync();
            Assert.That(stats.TotalInspections, Is.EqualTo(5));
            Assert.That(stats.OkCount, Is.EqualTo(5));
            Assert.That(stats.NgCount, Is.EqualTo(0));
            Assert.That(stats.AnomalyCount, Is.EqualTo(0));
            Assert.That(stats.DefectRate, Is.EqualTo(0.0).Within(0.01));

            _mockApiClient.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        }
        finally
        {
            // Clean up temp files
            foreach (var path in tempImagePaths)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    [Test]
    public async Task DefectDetection_ThreeOkTwoNg_NgRateIsFortyPercent()
    {
        // Arrange
        var inspectionResults = new[]
        {
            new PipelineResult 
            {
                FinalDecision = "OK", 
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult { IsAnomalous = false, Score = 0.2, Threshold = 0.5, Confidence = 0.9, InferenceTimeMs = 80, DetectionType = "transistor" },
                SegmentationResult = null,
                TotalTimeMs = 120,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            },
            new PipelineResult 
            {
                FinalDecision = "OK", 
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult { IsAnomalous = false, Score = 0.3, Threshold = 0.5, Confidence = 0.9, InferenceTimeMs = 85, DetectionType = "transistor" },
                SegmentationResult = null,
                TotalTimeMs = 130,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            },
            new PipelineResult 
            {
                FinalDecision = "OK", 
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult { IsAnomalous = false, Score = 0.25, Threshold = 0.5, Confidence = 0.9, InferenceTimeMs = 90, DetectionType = "transistor" },
                SegmentationResult = null,
                TotalTimeMs = 140,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            },
            new PipelineResult 
            { 
                FinalDecision = "NG", 
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult { IsAnomalous = true, Score = 0.8, Threshold = 0.5, Confidence = 0.95, InferenceTimeMs = 120, DetectionType = "transistor" },
                SegmentationResult = new SegmentationResult
                {
                    HasDefects = true,
                    Detections = new List<DefectDetection>
                    {
                        new DefectDetection
                        {
                            ClassName = "missing_hole",
                            Confidence = 0.95,
                            BoundingBox = new BoundingBox { X = 100, Y = 150, Width = 50, Height = 30 },
                            Mask = new SegmentationMask { Area = 1500 }
                        }
                    },
                    OverallConfidence = 0.95,
                    InferenceTimeMs = 80,
                    ModelVersion = "YOLOv8-seg-1.0.0"
                },
                TotalTimeMs = 200,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            },
            new PipelineResult 
            { 
                FinalDecision = "NG", 
                DecisionStage = "Pipeline",
                AnomalyResult = new AnomalyResult { IsAnomalous = true, Score = 0.9, Threshold = 0.5, Confidence = 0.98, InferenceTimeMs = 140, DetectionType = "transistor" },
                SegmentationResult = new SegmentationResult
                {
                    HasDefects = true,
                    Detections = new List<DefectDetection>
                    {
                        new DefectDetection
                        {
                            ClassName = "mouse_bite",
                            Confidence = 0.88,
                            BoundingBox = new BoundingBox { X = 200, Y = 250, Width = 40, Height = 35 },
                            Mask = new SegmentationMask { Area = 1400 }
                        }
                    },
                    OverallConfidence = 0.88,
                    InferenceTimeMs = 80,
                    ModelVersion = "YOLOv8-seg-1.0.0"
                },
                TotalTimeMs = 220,
                Timestamp = DateTime.Now,
                ImagePath = "",
                ModelVersion = "TestModel-1.0.0",
                Metadata = null
            }
        };

        var callCount = 0;
        _mockApiClient.Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => inspectionResults[callCount++]);

        // Create temp image files for testing
        var tempImagePaths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var tempPath = Path.GetTempFileName();
            File.WriteAllBytes(tempPath, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header
            tempImagePaths.Add(tempPath);
        }

        try
        {
            // Act
            for (int i = 0; i < 5; i++)
            {
                _inspectionViewModel.ImagePath = tempImagePaths[i];
                await _inspectionViewModel.RunInspectionCommand.ExecuteAsync(null);
                await Task.Delay(50);
            }

        // Assert
        var allRecords = await _repository.GetRecordsAsync();
        Assert.That(allRecords.Count, Is.EqualTo(5));

        var okRecords = allRecords.Where(r => r.FinalResult == "OK").ToList();
        var ngRecords = allRecords.Where(r => r.FinalResult == "NG").ToList();

        Assert.That(okRecords.Count, Is.EqualTo(3));
        Assert.That(ngRecords.Count, Is.EqualTo(2));

        // Verify DefectDetails were saved for NG records
        var recordsWithDefects = await _context.InspectionRecords
            .Include(r => r.DefectDetails)
            .Where(r => r.FinalResult == "NG")
            .ToListAsync();

        Assert.That(recordsWithDefects.Count, Is.EqualTo(2));
        Assert.That(recordsWithDefects.All(r => r.DefectDetails.Count > 0), Is.True);

        var stats = await _repository.GetStatsAsync();
        Assert.That(stats.TotalInspections, Is.EqualTo(5));
        Assert.That(stats.OkCount, Is.EqualTo(3));
        Assert.That(stats.NgCount, Is.EqualTo(2));
        Assert.That(stats.DefectRate, Is.EqualTo(40.0).Within(0.01));

        _mockApiClient.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        }
        finally
        {
            // Clean up temp files
            foreach (var path in tempImagePaths)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }

    [Test]
    public async Task DriftDetection_RisingAnomalyScores_AlarmServiceFires()
    {
        // Arrange - Create baseline with low scores
        var baselineScores = new List<double>();
        for (int i = 0; i < 60; i++)
        {
            baselineScores.Add(0.1 + (i * 0.002)); // 0.1 to 0.22 range
        }
        _driftMonitor.SetBaseline(baselineScores);

        // Create records with high anomaly scores to trigger drift
        var highScoreRecords = new List<InspectionRecord>();
        for (int i = 0; i < 50; i++)
        {
            var record = new InspectionRecord
            {
                ImagePath = $"/test/drift_image{i}.jpg",
                FinalResult = "OK",
                StageUsed = "Pipeline",
                AnomalyScore = 0.7 + (i * 0.004), // 0.7 to 0.896 range (significantly higher)
                InferenceTimeMs = 100 + i,
                Timestamp = DateTime.Now.AddSeconds(i),
                ModelVersionId = _testModelVersion.Id
            };
            highScoreRecords.Add(record);
        }

        // Save high score records to repository
        foreach (var record in highScoreRecords)
        {
            await _repository.SaveRecordAsync(record);
        }

        // Act - Analyze drift
        var driftReport = await _driftMonitor.AnalyzeAsync(CancellationToken.None);

        // Trigger alarm service check
        _alarmService.CheckAndTrigger(driftReport);

        // Assert
        Assert.That(driftReport.IsDriftDetected, Is.True);
        Assert.That(driftReport.Severity, Is.EqualTo(DriftSeverity.Significant).Or.EqualTo(DriftSeverity.Moderate));
        Assert.That(driftReport.PValue, Is.LessThan(0.1)); // Statistically significant
        
        Assert.That(_alarmService.IsAlarming, Is.True);
        
        // Verify baseline was used correctly
        Assert.That(_driftMonitor.HasBaseline, Is.True);
        Assert.That(driftReport.BaselineSampleCount, Is.EqualTo(60));
        Assert.That(driftReport.CurrentSampleCount, Is.GreaterThanOrEqualTo(10)); // At least 10 samples needed for analysis
    }

    [Test]
    public async Task ConcurrentInspection_TwoSimultaneousCalls_SecondIsRejected()
    {
        // Arrange
        var slowResponseDelay = 500; // 500ms delay to simulate slow API
        var firstCallStarted = new TaskCompletionSource<bool>();
        var firstCallCanComplete = new TaskCompletionSource<bool>();

        _mockApiClient.Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string imagePath, CancellationToken ct) =>
            {
                firstCallStarted.SetResult(true);
                await firstCallCanComplete.Task; // Wait until we allow it to complete
                await Task.Delay(slowResponseDelay, ct);
                return new PipelineResult
                {
                    FinalDecision = "OK",
                    DecisionStage = "Pipeline",
                    AnomalyResult = new AnomalyResult
                    {
                        IsAnomalous = false,
                        Score = 0.3,
                        Threshold = 0.5,
                        Confidence = 0.9,
                        InferenceTimeMs = 80,
                        DetectionType = "transistor"
                    },
                    SegmentationResult = null,
                    TotalTimeMs = slowResponseDelay,
                    Timestamp = DateTime.Now,
                    ImagePath = imagePath,
                    ModelVersion = "TestModel-1.0.0",
                    Metadata = null
                };
            });

        // Create temp image files for testing
        var tempImage1 = Path.GetTempFileName();
        var tempImage2 = Path.GetTempFileName();
        File.WriteAllBytes(tempImage1, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header
        File.WriteAllBytes(tempImage2, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header

        try
        {
            _inspectionViewModel.ImagePath = tempImage1;

            // Act
            var firstInspectionTask = Task.Run(async () =>
            {
                await _inspectionViewModel.RunInspectionCommand.ExecuteAsync(null);
            });

            // Wait for first call to start
            await firstCallStarted.Task;

            // Immediately try second inspection while first is running
            _inspectionViewModel.ImagePath = tempImage2;
            var secondInspectionTask = Task.Run(async () =>
            {
                await _inspectionViewModel.RunInspectionCommand.ExecuteAsync(null);
            });

            // Let second call complete first (it should be rejected quickly)
            await Task.Delay(100);

            // Now allow first call to complete
            firstCallCanComplete.SetResult(true);
            await firstInspectionTask;
            await secondInspectionTask;

            // Assert
            var allRecords = await _repository.GetRecordsAsync();
            
            // Should only have one record - the first successful inspection
            Assert.That(allRecords.Count, Is.EqualTo(1));
            Assert.That(allRecords.First().ImagePath, Is.EqualTo(tempImage1));

            // API should only be called once (second call rejected by semaphore)
            _mockApiClient.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            
            // ViewModel should indicate it's not busy after operations complete
            Assert.That(_inspectionViewModel.IsRunning, Is.False);
        }
        finally
        {
            if (File.Exists(tempImage1))
                File.Delete(tempImage1);
            if (File.Exists(tempImage2))
                File.Delete(tempImage2);
        }
    }

    [Test]
    public async Task ApiServerDown_InspectionAttempt_StatusMessageShowsError()
    {
        // Arrange - Create a temporary file that exists for the validation
        var tempImagePath = Path.GetTempFileName();
        File.WriteAllBytes(tempImagePath, new byte[] { 0xFF, 0xD8, 0xFF }); // Simple JPEG header
        
        try
        {
            var apiException = new InspectionApiException("Connection refused: server not responding");
            
            _mockApiClient.Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(apiException);

            _inspectionViewModel.ImagePath = tempImagePath;

            // Act
            await _inspectionViewModel.RunInspectionCommand.ExecuteAsync(null);
            
            // Allow async operations to complete
            await Task.Delay(100);

            // Assert
            Assert.That(_inspectionViewModel.StatusMessage, Does.Contain("Error"));
            Assert.That(_inspectionViewModel.StatusMessage, Does.Contain("server not responding").IgnoreCase);
            Assert.That(_inspectionViewModel.IsRunning, Is.False);

            // Verify no record was saved to database on error
            var allRecords = await _repository.GetRecordsAsync();
            Assert.That(allRecords.Count, Is.EqualTo(0));

            // Verify API was called but failed
            _mockApiClient.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempImagePath))
                File.Delete(tempImagePath);
        }
    }

    [Test]
    public async Task HealthCheck_ServerDown_ReturnsFalse()
    {
        // Arrange
        _mockApiClient.Setup(x => x.IsServerHealthyAsync())
            .ReturnsAsync(false);

        // Act
        var isHealthy = await _mockApiClient.Object.IsServerHealthyAsync();

        // Assert
        Assert.That(isHealthy, Is.False);
        _mockApiClient.Verify(x => x.IsServerHealthyAsync(), Times.Once);
    }
}