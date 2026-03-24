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
public class DashboardViewModelTests
{
    private Mock<IInspectionRepository> _mockRepository;
    private Mock<IAlarmService> _mockAlarmService;
    private DashboardViewModel _viewModel;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<IInspectionRepository>();
        _mockAlarmService = new Mock<IAlarmService>();

        _mockAlarmService.Setup(x => x.IsAlarming).Returns(false);

        _viewModel = new DashboardViewModel(_mockRepository.Object, _mockAlarmService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel?.Dispose();
    }

    [Test]
    public void Constructor_Initialized_DefaultValuesAreZero()
    {
        Assert.That(_viewModel.TotalCount, Is.EqualTo(0));
        Assert.That(_viewModel.NgCount, Is.EqualTo(0));
        Assert.That(_viewModel.NgRate, Is.EqualTo(0.0));
        Assert.That(_viewModel.AvgInferenceMs, Is.EqualTo(0.0));
        Assert.That(_viewModel.AvgAnomalyScore, Is.EqualTo(0.0));
        Assert.That(_viewModel.RecentRecords.Count, Is.EqualTo(0));
        Assert.That(_viewModel.IsAlarming, Is.False);
        Assert.That(_viewModel.AlarmMessage, Is.EqualTo("DRIFT DETECTED - Retraining Recommended"));
    }

    [Test]
    public async Task LoadDataAsync_EmptyDatabase_AllMetricsZero()
    {
        var emptyStats = new SessionStats
        {
            SessionStart = DateTime.Today,
            SessionEnd = DateTime.Now,
            TotalInspections = 0,
            OkCount = 0,
            AnomalyCount = 0,
            NgCount = 0,
            AverageInferenceTime = 0.0,
            MinInferenceTime = 0.0,
            MaxInferenceTime = 0.0,
            AverageAnomalyScore = 0.0,
            Uptime = TimeSpan.Zero
        };

        _mockRepository.Setup(x => x.GetStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(emptyStats);

        _mockRepository.Setup(x => x.GetRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 
                                                     It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<InspectionRecord>());

        await Task.Delay(100);

        Assert.That(_viewModel.TotalCount, Is.EqualTo(0));
        Assert.That(_viewModel.NgCount, Is.EqualTo(0));
        Assert.That(_viewModel.NgRate, Is.EqualTo(0.0));
    }

    [Test]
    public async Task LoadDataAsync_WithRecords_UpdatesStats()
    {
        _viewModel.Dispose();

        var stats = new SessionStats
        {
            SessionStart = DateTime.Today,
            SessionEnd = DateTime.Now,
            TotalInspections = 100,
            OkCount = 80,
            AnomalyCount = 10,
            NgCount = 10,
            AverageInferenceTime = 250.5,
            MinInferenceTime = 200.0,
            MaxInferenceTime = 300.0,
            AverageAnomalyScore = 0.123,
            Uptime = TimeSpan.FromHours(8)
        };

        var recentRecords = new List<InspectionRecord>
        {
            new InspectionRecord
            {
                Id = 1,
                Timestamp = DateTime.Now.AddMinutes(-5),
                ImagePath = "test1.jpg",
                FinalResult = "OK",
                StageUsed = "Anomaly",
                AnomalyScore = 0.1,
                InferenceTimeMs = 240,
                ModelVersionId = 1
            },
            new InspectionRecord
            {
                Id = 2,
                Timestamp = DateTime.Now.AddMinutes(-10),
                ImagePath = "test2.jpg",
                FinalResult = "NG",
                StageUsed = "Pipeline",
                AnomalyScore = 0.8,
                InferenceTimeMs = 280,
                ModelVersionId = 1
            }
        };

        _mockRepository.Setup(x => x.GetStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(stats);

        _mockRepository.Setup(x => x.GetRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 
                                                     It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(recentRecords);

        _viewModel = new DashboardViewModel(_mockRepository.Object, _mockAlarmService.Object);

        await Task.Delay(1000);

        Assert.That(_viewModel.TotalCount, Is.EqualTo(100));
        Assert.That(_viewModel.NgCount, Is.EqualTo(20));
        Assert.That(_viewModel.NgRate, Is.EqualTo(20.0));
        Assert.That(_viewModel.AvgInferenceMs, Is.EqualTo(250.5));
        Assert.That(_viewModel.AvgAnomalyScore, Is.EqualTo(0.123));
    }

    [Test]
    public void AlarmTriggered_WhenAlarmFires_IsAlarmingBecomesTrue()
    {
        _mockAlarmService.Setup(x => x.IsAlarming).Returns(true);

        var alarmEvent = new AlarmEvent
        {
            Id = "test-alarm-001",
            Timestamp = DateTime.Now,
            Severity = AlarmSeverity.Critical,
            Type = AlarmType.DriftDetected,
            Message = "Test drift detected",
            Source = "DriftAnalyzer"
        };

        _mockAlarmService.Raise(x => x.AlarmTriggered += null, _mockAlarmService.Object, alarmEvent);

        Assert.That(_viewModel.IsAlarming, Is.True);
        Assert.That(_viewModel.AlarmMessage, Is.EqualTo("Test drift detected"));
    }

    [Test]
    public void DismissAlarmCommand_WhenAlarming_CallsAlarmServiceReset()
    {
        _viewModel.IsAlarming = true;

        _viewModel.DismissAlarmCommand.Execute(null);

        _mockAlarmService.Verify(x => x.Reset(), Times.Once);
        Assert.That(_viewModel.IsAlarming, Is.False);
    }

    [Test]
    public async Task LoadDataAsync_WithNgRecords_CalculatesCorrectNgRate()
    {
        _viewModel.Dispose();

        var stats = new SessionStats
        {
            SessionStart = DateTime.Today,
            SessionEnd = DateTime.Now,
            TotalInspections = 50,
            OkCount = 30,
            AnomalyCount = 5,
            NgCount = 15,
            AverageInferenceTime = 200.0,
            MinInferenceTime = 150.0,
            MaxInferenceTime = 250.0,
            AverageAnomalyScore = 0.25,
            Uptime = TimeSpan.FromHours(4)
        };

        _mockRepository.Setup(x => x.GetStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(stats);

        _mockRepository.Setup(x => x.GetRecordsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 
                                                     It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<InspectionRecord>());

        _viewModel = new DashboardViewModel(_mockRepository.Object, _mockAlarmService.Object);

        await Task.Delay(1000);

        Assert.That(_viewModel.NgCount, Is.EqualTo(20));
        Assert.That(_viewModel.NgRate, Is.EqualTo(40.0));
    }
}