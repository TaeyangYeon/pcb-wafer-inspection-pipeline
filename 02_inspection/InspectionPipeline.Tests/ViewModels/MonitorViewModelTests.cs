using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.UI.ViewModels;

namespace InspectionPipeline.Tests.ViewModels;

[TestFixture]
public class MonitorViewModelTests
{
    private Mock<IDriftMonitor> _mockDriftMonitor;
    private Mock<IInspectionRepository> _mockRepository;
    private Mock<IAlarmService> _mockAlarmService;
    private MonitorViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _mockDriftMonitor = new Mock<IDriftMonitor>();
        _mockRepository = new Mock<IInspectionRepository>();
        _mockAlarmService = new Mock<IAlarmService>();

        _mockDriftMonitor.Setup(d => d.HasBaseline).Returns(false);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(It.IsAny<int>()))
                      .ReturnsAsync(new List<double>());

        _viewModel = new MonitorViewModel(_mockDriftMonitor.Object, _mockRepository.Object, _mockAlarmService.Object);
    }

    [Test]
    public void Constructor_Initialized_DriftStatusIsEmpty()
    {
        // Arrange & Act is done in SetUp

        // Assert
        Assert.That(_viewModel.DriftStatus, Is.EqualTo("Insufficient Data"));
        Assert.That(_viewModel.LastAnalyzedAt, Is.EqualTo("Never"));
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Ready"));
        Assert.That(_viewModel.HasBaseline, Is.False);
        Assert.That(_viewModel.IsRetrainingRecommended, Is.False);
        Assert.That(_viewModel.BaselineInfo, Is.EqualTo("No baseline set"));
    }

    [Test]
    public async Task RunAnalysisCommand_InsufficientData_ShowsInsufficientStatus()
    {
        // Arrange
        _mockDriftMonitor.Setup(d => d.HasBaseline).Returns(true);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(100))
                      .ReturnsAsync(new List<double> { 0.1, 0.2 }); // Only 2 scores, need 10

        // Act
        await _viewModel.RunAnalysisCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Error: Insufficient data for analysis (need at least 10 scores)"));
        _mockDriftMonitor.Verify(d => d.AnalyzeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAnalysisCommand_StableDistribution_ShowsStableStatus()
    {
        // Arrange
        _mockDriftMonitor.Setup(d => d.HasBaseline).Returns(true);
        
        var scores = new List<double>();
        for (int i = 0; i < 15; i++) scores.Add(0.5 + i * 0.01); // 15 scores

        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(100))
                      .ReturnsAsync(scores);

        var stableReport = new DriftReport
        {
            Timestamp = DateTime.Now,
            PValue = 0.8,
            KsStatistic = 0.1,
            IsDriftDetected = false,
            Severity = DriftSeverity.None,
            BaselineSampleCount = 100,
            CurrentSampleCount = 15,
            BaselineStats = CreateMockStats(),
            CurrentStats = CreateMockStats()
        };

        _mockDriftMonitor.Setup(d => d.AnalyzeAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(stableReport);

        // Act
        await _viewModel.RunAnalysisCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.DriftStatus, Is.EqualTo("Stable"));
        Assert.That(_viewModel.IsRetrainingRecommended, Is.False);
        Assert.That(_viewModel.StatusMessage, Does.Contain("Analysis complete - Status: Stable"));
        Assert.That(_viewModel.KsStatistic, Is.EqualTo(0.1));
        Assert.That(_viewModel.PValue, Is.EqualTo(0.8));
    }

    [Test]
    public async Task RunAnalysisCommand_Drifted_IsRetrainingRecommendedTrue()
    {
        // Arrange
        _mockDriftMonitor.Setup(d => d.HasBaseline).Returns(true);
        
        var scores = new List<double>();
        for (int i = 0; i < 15; i++) scores.Add(0.8 + i * 0.01); // 15 scores, shifted high

        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(100))
                      .ReturnsAsync(scores);

        var driftedReport = new DriftReport
        {
            Timestamp = DateTime.Now,
            PValue = 0.001,
            KsStatistic = 0.9,
            IsDriftDetected = true,
            Severity = DriftSeverity.Significant,
            BaselineSampleCount = 100,
            CurrentSampleCount = 15,
            BaselineStats = CreateMockStats(),
            CurrentStats = CreateMockStats()
        };

        _mockDriftMonitor.Setup(d => d.AnalyzeAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(driftedReport);

        // Act
        await _viewModel.RunAnalysisCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.DriftStatus, Is.EqualTo("Retraining Recommended"));
        Assert.That(_viewModel.IsRetrainingRecommended, Is.True);
        Assert.That(_viewModel.StatusMessage, Does.Contain("Analysis complete - Status: Retraining Recommended"));
        Assert.That(_viewModel.KsStatistic, Is.EqualTo(0.9));
        Assert.That(_viewModel.PValue, Is.EqualTo(0.001));
    }

    [Test]
    public async Task SetBaselineCommand_WithScores_CallsSetBaseline()
    {
        // Arrange
        var baselineScores = new List<double>();
        for (int i = 0; i < 60; i++) baselineScores.Add(0.3 + i * 0.01); // 60 scores, need 50

        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200))
                      .ReturnsAsync(baselineScores);

        // Act
        await _viewModel.SetBaselineCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Does.Contain($"Baseline set with {baselineScores.Count} samples"));
        Assert.That(_viewModel.HasBaseline, Is.True);
        _mockDriftMonitor.Verify(d => d.SetBaseline(baselineScores), Times.Once);
    }

    [Test]
    public async Task SetBaselineCommand_InsufficientScores_ShowsErrorMessage()
    {
        // Arrange
        var insufficientScores = new List<double> { 0.1, 0.2, 0.3 }; // Only 3 scores, need 50

        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200))
                      .ReturnsAsync(insufficientScores);

        // Act
        await _viewModel.SetBaselineCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Error: Insufficient data for baseline (need at least 50 scores)"));
        _mockDriftMonitor.Verify(d => d.SetBaseline(It.IsAny<List<double>>()), Times.Never);
    }

    [Test]
    public async Task RunAnalysisCommand_NoBaseline_ShowsBaselineError()
    {
        // Arrange
        _mockDriftMonitor.Setup(d => d.HasBaseline).Returns(false);

        // Act
        await _viewModel.RunAnalysisCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.StatusMessage, Is.EqualTo("Error: No baseline set. Please set a baseline first."));
        // Note: GetAnomalyScoresAsync is called by constructor's LoadScoreDataAsync, so we verify it was called at most once more
        _mockRepository.Verify(r => r.GetAnomalyScoresAsync(100), Times.AtMost(1));
        _mockDriftMonitor.Verify(d => d.AnalyzeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DistributionStats CreateMockStats()
    {
        return new DistributionStats
        {
            Mean = 0.5,
            StandardDeviation = 0.1,
            Minimum = 0.0,
            Maximum = 1.0,
            Q25 = 0.4,
            Median = 0.5,
            Q75 = 0.6
        };
    }
}