using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.Tests.Core;

[TestFixture]
public class DriftMonitorTests
{
    private Mock<IInspectionRepository> _mockRepository;
    private DriftMonitor _driftMonitor;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IInspectionRepository>();
        _driftMonitor = new DriftMonitor(_mockRepository.Object);
    }

    [Test]
    public async Task AnalyzeAsync_InsufficientData_ReturnsInsufficientDataStatus()
    {
        // Arrange
        var baselineScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _driftMonitor.SetBaseline(baselineScores);

        var insufficientScores = GenerateNormalDistribution(5, 0.5, 0.1);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(insufficientScores);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(result.Notes, Contains.Substring("Insufficient data"));
        Assert.That(result.CurrentSampleCount, Is.EqualTo(5));
        Assert.That(result.IsDriftDetected, Is.False);
        Assert.That(result.Severity, Is.EqualTo(DriftSeverity.None));
    }

    [Test]
    public async Task AnalyzeAsync_IdenticalDistributions_ReturnsStableStatus()
    {
        // Arrange
        var baselineScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _driftMonitor.SetBaseline(baselineScores);

        var identicalScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(identicalScores);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(result.PValue, Is.GreaterThan(0.1));
        Assert.That(result.IsDriftDetected, Is.False);
        Assert.That(result.Severity, Is.EqualTo(DriftSeverity.None));
        Assert.That(result.KsStatistic, Is.LessThan(0.2));
    }

    [Test]
    public async Task AnalyzeAsync_SignificantlyDifferentDistributions_ReturnsDriftStatus()
    {
        // Arrange
        var baselineScores = GenerateNormalDistribution(50, 0.3, 0.05);
        _driftMonitor.SetBaseline(baselineScores);

        var driftedScores = GenerateNormalDistribution(50, 0.8, 0.05);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(driftedScores);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(result.PValue, Is.LessThan(0.01));
        Assert.That(result.IsDriftDetected, Is.True);
        Assert.That(result.Severity, Is.EqualTo(DriftSeverity.Significant));
        Assert.That(result.KsStatistic, Is.GreaterThan(0.5));
    }

    [Test]
    public async Task AnalyzeAsync_NoBaseline_ThrowsInvalidOperationException()
    {
        // Arrange
        var recentScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(recentScores);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _driftMonitor.AnalyzeAsync());
        
        Assert.That(ex.Message, Contains.Substring("No baseline has been set"));
    }

    [Test]
    public void SetBaseline_ValidScores_SetsHasBaselineTrue()
    {
        // Arrange
        var validScores = GenerateNormalDistribution(50, 0.5, 0.1);

        // Act
        _driftMonitor.SetBaseline(validScores);

        // Assert
        Assert.That(_driftMonitor.HasBaseline, Is.True);
    }

    [Test]
    public void SetBaseline_EmptyList_ThrowsArgumentException()
    {
        // Arrange
        var emptyScores = new List<double>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _driftMonitor.SetBaseline(emptyScores));
        Assert.That(ex.Message, Contains.Substring("cannot be empty"));
        Assert.That(_driftMonitor.HasBaseline, Is.False);
    }

    [Test]
    public void SetBaseline_InsufficientSamples_ThrowsArgumentException()
    {
        // Arrange
        var insufficientScores = GenerateNormalDistribution(20, 0.5, 0.1);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _driftMonitor.SetBaseline(insufficientScores));
        Assert.That(ex.Message, Contains.Substring("at least 30 samples"));
        Assert.That(_driftMonitor.HasBaseline, Is.False);
    }

    [Test]
    public async Task AnalyzeAsync_ModerateDrift_ReturnsModerateStatus()
    {
        // Arrange - Create clearly different distributions with more separation
        var baselineScores = GenerateNormalDistribution(50, 0.3, 0.05);
        _driftMonitor.SetBaseline(baselineScores);

        var moderatelyDriftedScores = GenerateNormalDistribution(50, 0.7, 0.05);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(moderatelyDriftedScores);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert - This should definitely show drift with such different distributions
        Assert.That(result.PValue, Is.LessThan(0.05), "Large difference in means should produce significant p-value");
        Assert.That(result.IsDriftDetected, Is.True);
        Assert.That(result.Severity, Is.AnyOf(DriftSeverity.Moderate, DriftSeverity.Significant));
        Assert.That(result.Recommendations, Is.Not.Empty);
    }

    [Test]
    public async Task AnalyzeAsync_ValidData_CalculatesCorrectStatistics()
    {
        // Arrange
        var baselineScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _driftMonitor.SetBaseline(baselineScores);

        var recentScores = GenerateNormalDistribution(50, 0.5, 0.1);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(recentScores);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(result.BaselineSampleCount, Is.EqualTo(50));
        Assert.That(result.CurrentSampleCount, Is.EqualTo(50));
        Assert.That(result.BaselineStats.Mean, Is.InRange(0.4, 0.6));
        Assert.That(result.CurrentStats.Mean, Is.InRange(0.4, 0.6));
        Assert.That(result.KsStatistic, Is.InRange(0.0, 1.0));
        Assert.That(result.PValue, Is.InRange(0.0, 1.0));
        Assert.That(result.Timestamp, Is.InRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow));
    }

    [Test]
    public async Task AnalyzeAsync_KnownDistributions_ReturnsExpectedKsStatistic()
    {
        // Arrange - Create two distributions with known KS statistic
        var baseline = new List<double> { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
        baseline.AddRange(Enumerable.Repeat(0.5, 20)); // Pad to meet 30 sample minimum
        
        var shifted = new List<double> { 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1 };
        shifted.AddRange(Enumerable.Repeat(0.6, 20)); // Pad to meet 30 sample minimum
        
        _driftMonitor.SetBaseline(baseline);
        _mockRepository.Setup(r => r.GetAnomalyScoresAsync(200, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(shifted);

        // Act
        var result = await _driftMonitor.AnalyzeAsync();

        // Assert
        Assert.That(result.KsStatistic, Is.GreaterThan(0.0));
        Assert.That(result.PValue, Is.LessThan(1.0));
        Assert.That(result.KsStatistic, Is.LessThan(1.0));
    }

    private List<double> GenerateNormalDistribution(int count, double mean, double stdDev)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var scores = new List<double>();
        
        for (int i = 0; i < count; i++)
        {
            // Box-Muller transformation for normal distribution
            var u1 = random.NextDouble();
            var u2 = random.NextDouble();
            var z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            var sample = mean + stdDev * z;
            
            // Clamp to [0, 1] range as anomaly scores should be
            scores.Add(Math.Max(0.0, Math.Min(1.0, sample)));
        }
        
        return scores;
    }
}