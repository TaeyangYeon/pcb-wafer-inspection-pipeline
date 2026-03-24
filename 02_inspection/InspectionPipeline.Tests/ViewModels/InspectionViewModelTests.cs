using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.UI.ViewModels;
using Moq;
using NUnit.Framework;

namespace InspectionPipeline.Tests.ViewModels;

[TestFixture]
public class InspectionViewModelTests
{
    private Mock<IInspectionApiClient> _mockApiClient;
    private Mock<IInspectionRepository> _mockRepository;
    private Mock<IImageOverlayRenderer> _mockOverlayRenderer;
    private InspectionViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _mockApiClient = new Mock<IInspectionApiClient>();
        _mockRepository = new Mock<IInspectionRepository>();
        _mockOverlayRenderer = new Mock<IImageOverlayRenderer>();

        _viewModel = new InspectionViewModel(
            _mockApiClient.Object,
            _mockRepository.Object,
            _mockOverlayRenderer.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel?.Dispose();
    }

    [Test]
    public void Constructor_Initialized_StatusMessageIsReady()
    {
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Ready"));
        Assert.That(_viewModel.IsRunning, Is.False);
        Assert.That(_viewModel.ImagePath, Is.EqualTo(""));
        Assert.That(_viewModel.FinalResult, Is.EqualTo(""));
        Assert.That(_viewModel.StageUsed, Is.EqualTo(""));
        Assert.That(_viewModel.AnomalyScore, Is.EqualTo(0.0));
        Assert.That(_viewModel.InferenceTimeMs, Is.EqualTo(0.0));
        Assert.That(_viewModel.Detections.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task RunInspectionCommand_NoImageLoaded_DoesNotCallApi()
    {
        await _viewModel.RunInspectionCommand.ExecuteAsync(null);

        _mockApiClient.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
                             Times.Never);
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Error: No image selected"));
    }

    [Test]
    public async Task RunInspectionCommand_ApiReturnsOk_SetsResultOk()
    {
        var testImagePath = "test_ok.jpg";
        var pipelineResult = CreateTestPipelineResult("OK", "Anomaly", 0.15, 200.0);

        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(pipelineResult);

            _mockOverlayRenderer.Setup(x => x.RenderCombined(It.IsAny<byte[]>(), It.IsAny<PipelineResult>()))
                               .Returns(new byte[] { 1, 2, 3 });

            _mockRepository.Setup(x => x.SaveRecordAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new InspectionRecord { Id = 1, ImagePath = testImagePath, FinalResult = "OK", 
                                                              StageUsed = "Anomaly", AnomalyScore = 0.15, 
                                                              InferenceTimeMs = 200.0, ModelVersionId = 1, 
                                                              Timestamp = DateTime.Now });

            await _viewModel.RunInspectionCommand.ExecuteAsync(null);

            Assert.That(_viewModel.FinalResult, Is.EqualTo("OK"));
            Assert.That(_viewModel.StageUsed, Is.EqualTo("Anomaly"));
            Assert.That(_viewModel.AnomalyScore, Is.EqualTo(0.15));
            Assert.That(_viewModel.InferenceTimeMs, Is.EqualTo(200.0));
            Assert.That(_viewModel.StatusMessage, Is.EqualTo("Inspection complete"));
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Test]
    public async Task RunInspectionCommand_ApiReturnsNg_SetsResultNg()
    {
        var testImagePath = "test_ng.jpg";
        var pipelineResult = CreateTestPipelineResult("NG", "Pipeline", 0.95, 350.0);
        pipelineResult.SegmentationResult = new SegmentationResult
        {
            HasDefects = true,
            Detections = new List<DefectDetection>
            {
                new DefectDetection
                {
                    ClassName = "missing_hole",
                    Confidence = 0.85,
                    BoundingBox = new BoundingBox { X = 100, Y = 200, Width = 50, Height = 40 },
                    Mask = new SegmentationMask { Area = 2000 }
                }
            },
            OverallConfidence = 0.85,
            InferenceTimeMs = 150.0,
            ModelVersion = "yolov8_v1.0"
        };

        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(pipelineResult);

            _mockOverlayRenderer.Setup(x => x.RenderCombined(It.IsAny<byte[]>(), It.IsAny<PipelineResult>()))
                               .Returns(new byte[] { 4, 5, 6 });

            _mockRepository.Setup(x => x.SaveRecordAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new InspectionRecord { Id = 2, ImagePath = testImagePath, FinalResult = "NG", 
                                                              StageUsed = "Pipeline", AnomalyScore = 0.95, 
                                                              InferenceTimeMs = 350.0, ModelVersionId = 1, 
                                                              Timestamp = DateTime.Now });

            await _viewModel.RunInspectionCommand.ExecuteAsync(null);

            Assert.That(_viewModel.FinalResult, Is.EqualTo("NG"));
            Assert.That(_viewModel.StageUsed, Is.EqualTo("Pipeline"));
            Assert.That(_viewModel.AnomalyScore, Is.EqualTo(0.95));
            Assert.That(_viewModel.InferenceTimeMs, Is.EqualTo(350.0));
            Assert.That(_viewModel.Detections.Count, Is.EqualTo(1));
            Assert.That(_viewModel.Detections[0].ClassName, Is.EqualTo("missing_hole"));
            Assert.That(_viewModel.Detections[0].Confidence, Is.EqualTo(0.85));
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Test]
    public async Task RunInspectionCommand_ApiReturnsAnomaly_SetsResultAnomaly()
    {
        var testImagePath = "test_anomaly.jpg";
        var pipelineResult = CreateTestPipelineResult("ANOMALY", "Anomaly", 0.75, 180.0);

        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(pipelineResult);

            _mockOverlayRenderer.Setup(x => x.RenderCombined(It.IsAny<byte[]>(), It.IsAny<PipelineResult>()))
                               .Returns(new byte[] { 7, 8, 9 });

            _mockRepository.Setup(x => x.SaveRecordAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new InspectionRecord { Id = 3, ImagePath = testImagePath, FinalResult = "ANOMALY", 
                                                              StageUsed = "Anomaly", AnomalyScore = 0.75, 
                                                              InferenceTimeMs = 180.0, ModelVersionId = 1, 
                                                              Timestamp = DateTime.Now });

            await _viewModel.RunInspectionCommand.ExecuteAsync(null);

            Assert.That(_viewModel.FinalResult, Is.EqualTo("ANOMALY"));
            Assert.That(_viewModel.StageUsed, Is.EqualTo("Anomaly"));
            Assert.That(_viewModel.AnomalyScore, Is.EqualTo(0.75));
            Assert.That(_viewModel.InferenceTimeMs, Is.EqualTo(180.0));
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Test]
    public async Task RunInspectionCommand_WhileRunning_SecondCallIgnored()
    {
        var testImagePath = "test_concurrent.jpg";
        
        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            var taskCompletionSource = new TaskCompletionSource<PipelineResult>();
            
            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .Returns(taskCompletionSource.Task);

            _mockOverlayRenderer.Setup(x => x.RenderCombined(It.IsAny<byte[]>(), It.IsAny<PipelineResult>()))
                               .Returns(new byte[] { 1, 2, 3 });

            _mockRepository.Setup(x => x.SaveRecordAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(new InspectionRecord { Id = 1, ImagePath = testImagePath, FinalResult = "OK", 
                                                              StageUsed = "Anomaly", AnomalyScore = 0.1, 
                                                              InferenceTimeMs = 200.0, ModelVersionId = 1, 
                                                              Timestamp = DateTime.Now });

            var firstTask = _viewModel.RunInspectionCommand.ExecuteAsync(null);
            await Task.Delay(100);
            
            Assert.That(_viewModel.IsRunning, Is.True);
            Assert.That(_viewModel.StatusMessage, Is.EqualTo("Running inspection..."));

            var secondTask = _viewModel.RunInspectionCommand.ExecuteAsync(null);
            await Task.Delay(50);

            taskCompletionSource.SetResult(CreateTestPipelineResult("OK", "Anomaly", 0.1, 200));

            await firstTask;
            await secondTask;

            _mockApiClient.Verify(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()), 
                                 Times.AtLeastOnce());
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Test]
    public async Task CancelCommand_WhileRunning_CancelsInference()
    {
        var testImagePath = "test_cancel.jpg";
        
        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            var taskCompletionSource = new TaskCompletionSource<PipelineResult>();

            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .Returns((string path, CancellationToken ct) =>
                          {
                              ct.Register(() => taskCompletionSource.SetCanceled(ct));
                              return taskCompletionSource.Task;
                          });

            var inspectionTask = _viewModel.RunInspectionCommand.ExecuteAsync(null);
            await Task.Delay(100);

            Assert.That(_viewModel.IsRunning, Is.True);

            _viewModel.CancelCommand.Execute(null);
            await Task.Delay(100);

            Assert.That(_viewModel.StatusMessage, Is.EqualTo("Cancelling...").Or.EqualTo("Inspection cancelled"));

            try
            {
                await inspectionTask;
            }
            catch (OperationCanceledException)
            {
            }

            Assert.That(_viewModel.IsRunning, Is.False);
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Test]
    public async Task RunInspectionCommand_ApiThrows_StatusMessageShowsError()
    {
        var testImagePath = "test.jpg";
        
        using (var tempFile = File.Create(testImagePath))
        {
            tempFile.WriteByte(1);
        }
        
        try
        {
            _viewModel.ImagePath = testImagePath;

            _mockApiClient.Setup(x => x.InspectAsync(testImagePath, It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new InvalidOperationException("Network error"));

            await _viewModel.RunInspectionCommand.ExecuteAsync(null);

            Assert.That(_viewModel.StatusMessage, Does.StartWith("Error:"));
            Assert.That(_viewModel.StatusMessage, Does.Contain("Network error"));
            Assert.That(_viewModel.IsRunning, Is.False);
        }
        finally
        {
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    private PipelineResult CreateTestPipelineResult(string finalDecision, string decisionStage, 
                                                   double anomalyScore, double totalTimeMs)
    {
        return new PipelineResult
        {
            FinalDecision = finalDecision,
            DecisionStage = decisionStage,
            AnomalyResult = new AnomalyResult
            {
                IsAnomalous = anomalyScore > 0.5,
                Score = anomalyScore,
                Threshold = 0.5,
                Confidence = 0.95,
                InferenceTimeMs = totalTimeMs * 0.6,
                DetectionType = "transistor"
            },
            TotalTimeMs = totalTimeMs,
            Timestamp = DateTime.Now,
            ImagePath = "test.jpg",
            ModelVersion = "patchcore_v1.0"
        };
    }
}