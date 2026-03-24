using System;
using System.Threading;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

public class AlarmService : IAlarmService
{
    private readonly object _lock = new();
    private bool _isAlarming = false;

    public event EventHandler<AlarmEvent>? AlarmTriggered;

    public bool IsAlarming
    {
        get
        {
            lock (_lock)
            {
                return _isAlarming;
            }
        }
    }

    public void CheckAndTrigger(DriftReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        AlarmEvent? alarmEvent = null;

        if (report.Severity == DriftSeverity.Significant)
        {
            alarmEvent = CreateAlarmEvent(
                AlarmSeverity.Critical,
                "Critical drift detected - Model retraining recommended",
                report,
                "Immediate model retraining and investigation required"
            );
        }
        else if (report.Severity == DriftSeverity.Moderate)
        {
            alarmEvent = CreateAlarmEvent(
                AlarmSeverity.Warning,
                "Moderate drift detected - Monitor closely",
                report,
                "Monitor model performance and investigate potential causes"
            );
        }

        if (alarmEvent != null)
        {
            lock (_lock)
            {
                _isAlarming = true;
            }
            
            OnAlarmTriggered(alarmEvent);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isAlarming = false;
        }
    }

    private AlarmEvent CreateAlarmEvent(
        AlarmSeverity severity,
        string message,
        DriftReport report,
        string recommendedAction)
    {
        return new AlarmEvent
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Type = AlarmType.DriftDetected,
            Message = message,
            Source = "DriftMonitor",
            DriftReport = report,
            TriggerValue = report.PValue,
            ThresholdValue = severity == AlarmSeverity.Critical ? 0.01 : 0.05,
            RecommendedAction = recommendedAction,
            IsAcknowledged = false,
            AcknowledgedAt = null,
            AcknowledgedBy = null
        };
    }

    private void OnAlarmTriggered(AlarmEvent alarmEvent)
    {
        var handler = AlarmTriggered;
        if (handler != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    handler(this, alarmEvent);
                }
                catch (Exception)
                {
                }
            });
        }
    }
}