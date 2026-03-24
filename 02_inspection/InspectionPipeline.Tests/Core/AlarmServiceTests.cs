using System;
using System.Threading;
using NUnit.Framework;
using InspectionPipeline.Core.Models;
using InspectionPipeline.Core.Services;

namespace InspectionPipeline.Tests.Core;

[TestFixture]
public class AlarmServiceTests
{
    private AlarmService _alarmService;

    [SetUp]
    public void SetUp()
    {
        _alarmService = new AlarmService();
    }

    [Test]
    public void CheckAndTrigger_CriticalDrift_SetsIsAlarmingTrue()
    {
        // Arrange
        var criticalDriftReport = CreateDriftReport(DriftSeverity.Significant, 0.005);

        // Act
        _alarmService.CheckAndTrigger(criticalDriftReport);

        // Assert
        Assert.That(_alarmService.IsAlarming, Is.True);
    }

    [Test]
    public void CheckAndTrigger_CriticalDrift_FiresAlarmTriggeredEvent()
    {
        // Arrange
        var criticalDriftReport = CreateDriftReport(DriftSeverity.Significant, 0.005);
        AlarmEvent? firedEvent = null;
        var eventFired = new ManualResetEventSlim(false);

        _alarmService.AlarmTriggered += (sender, e) =>
        {
            firedEvent = e;
            eventFired.Set();
        };

        // Act
        _alarmService.CheckAndTrigger(criticalDriftReport);

        // Assert
        Assert.That(eventFired.Wait(1000), Is.True);
        Assert.That(firedEvent, Is.Not.Null);
        Assert.That(firedEvent.Severity, Is.EqualTo(AlarmSeverity.Critical));
        Assert.That(firedEvent.Type, Is.EqualTo(AlarmType.DriftDetected));
        Assert.That(firedEvent.Message, Contains.Substring("Critical drift"));
        Assert.That(firedEvent.DriftReport, Is.EqualTo(criticalDriftReport));
        Assert.That(firedEvent.TriggerValue, Is.EqualTo(0.005));
        Assert.That(firedEvent.ThresholdValue, Is.EqualTo(0.01));
    }

    [Test]
    public void CheckAndTrigger_ModerateDrift_SetsIsAlarmingTrue()
    {
        // Arrange
        var moderateDriftReport = CreateDriftReport(DriftSeverity.Moderate, 0.03);

        // Act
        _alarmService.CheckAndTrigger(moderateDriftReport);

        // Assert
        Assert.That(_alarmService.IsAlarming, Is.True);
    }

    [Test]
    public void CheckAndTrigger_ModerateDrift_FiresWarningAlarmEvent()
    {
        // Arrange
        var moderateDriftReport = CreateDriftReport(DriftSeverity.Moderate, 0.03);
        AlarmEvent? firedEvent = null;
        var eventFired = new ManualResetEventSlim(false);

        _alarmService.AlarmTriggered += (sender, e) =>
        {
            firedEvent = e;
            eventFired.Set();
        };

        // Act
        _alarmService.CheckAndTrigger(moderateDriftReport);

        // Assert
        Assert.That(eventFired.Wait(1000), Is.True);
        Assert.That(firedEvent, Is.Not.Null);
        Assert.That(firedEvent.Severity, Is.EqualTo(AlarmSeverity.Warning));
        Assert.That(firedEvent.Type, Is.EqualTo(AlarmType.DriftDetected));
        Assert.That(firedEvent.Message, Contains.Substring("Moderate drift"));
        Assert.That(firedEvent.TriggerValue, Is.EqualTo(0.03));
        Assert.That(firedEvent.ThresholdValue, Is.EqualTo(0.05));
    }

    [Test]
    public void CheckAndTrigger_StableDistribution_DoesNotFireEvent()
    {
        // Arrange
        var stableDriftReport = CreateDriftReport(DriftSeverity.None, 0.15);
        var eventFired = false;

        _alarmService.AlarmTriggered += (sender, e) => eventFired = true;

        // Act
        _alarmService.CheckAndTrigger(stableDriftReport);

        // Assert
        Assert.That(eventFired, Is.False);
        Assert.That(_alarmService.IsAlarming, Is.False);
    }

    [Test]
    public void CheckAndTrigger_MinorDrift_DoesNotFireEvent()
    {
        // Arrange
        var minorDriftReport = CreateDriftReport(DriftSeverity.Minor, 0.08);
        var eventFired = false;

        _alarmService.AlarmTriggered += (sender, e) => eventFired = true;

        // Act
        _alarmService.CheckAndTrigger(minorDriftReport);

        // Assert
        Assert.That(eventFired, Is.False);
        Assert.That(_alarmService.IsAlarming, Is.False);
    }

    [Test]
    public void Reset_AfterAlarm_SetsIsAlarmingFalse()
    {
        // Arrange
        var criticalDriftReport = CreateDriftReport(DriftSeverity.Significant, 0.005);
        _alarmService.CheckAndTrigger(criticalDriftReport);
        Assert.That(_alarmService.IsAlarming, Is.True);

        // Act
        _alarmService.Reset();

        // Assert
        Assert.That(_alarmService.IsAlarming, Is.False);
    }

    [Test]
    public void CheckAndTrigger_AfterReset_CanTriggerAgain()
    {
        // Arrange
        var criticalDriftReport = CreateDriftReport(DriftSeverity.Significant, 0.005);
        _alarmService.CheckAndTrigger(criticalDriftReport);
        _alarmService.Reset();
        
        var eventFiredCount = 0;
        _alarmService.AlarmTriggered += (sender, e) => Interlocked.Increment(ref eventFiredCount);

        // Act
        _alarmService.CheckAndTrigger(criticalDriftReport);

        // Wait for event to fire
        Thread.Sleep(100);

        // Assert
        Assert.That(_alarmService.IsAlarming, Is.True);
        Assert.That(eventFiredCount, Is.EqualTo(1));
    }

    [Test]
    public void CheckAndTrigger_NullReport_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _alarmService.CheckAndTrigger(null!));
        Assert.That(ex.ParamName, Is.EqualTo("report"));
    }

    [Test]
    public void CheckAndTrigger_MultipleEventHandlers_CallsAllHandlers()
    {
        // Arrange
        var criticalDriftReport = CreateDriftReport(DriftSeverity.Significant, 0.005);
        var firstHandlerCalled = false;
        var secondHandlerCalled = false;
        var allEventsComplete = new ManualResetEventSlim(false);
        var callCount = 0;

        _alarmService.AlarmTriggered += (sender, e) =>
        {
            firstHandlerCalled = true;
            if (Interlocked.Increment(ref callCount) == 2)
                allEventsComplete.Set();
        };
        
        _alarmService.AlarmTriggered += (sender, e) =>
        {
            secondHandlerCalled = true;
            if (Interlocked.Increment(ref callCount) == 2)
                allEventsComplete.Set();
        };

        // Act
        _alarmService.CheckAndTrigger(criticalDriftReport);

        // Assert
        Assert.That(allEventsComplete.Wait(2000), Is.True);
        Assert.That(firstHandlerCalled, Is.True);
        Assert.That(secondHandlerCalled, Is.True);
        Assert.That(_alarmService.IsAlarming, Is.True);
    }

    private DriftReport CreateDriftReport(DriftSeverity severity, double pValue)
    {
        return new DriftReport
        {
            Timestamp = DateTime.UtcNow,
            PValue = pValue,
            KsStatistic = 0.5,
            IsDriftDetected = severity != DriftSeverity.None,
            Severity = severity,
            BaselineSampleCount = 50,
            CurrentSampleCount = 50,
            BaselineStats = new DistributionStats
            {
                Mean = 0.5,
                StandardDeviation = 0.1,
                Minimum = 0.0,
                Maximum = 1.0,
                Q25 = 0.4,
                Median = 0.5,
                Q75 = 0.6
            },
            CurrentStats = new DistributionStats
            {
                Mean = 0.6,
                StandardDeviation = 0.12,
                Minimum = 0.1,
                Maximum = 0.9,
                Q25 = 0.5,
                Median = 0.6,
                Q75 = 0.7
            }
        };
    }
}