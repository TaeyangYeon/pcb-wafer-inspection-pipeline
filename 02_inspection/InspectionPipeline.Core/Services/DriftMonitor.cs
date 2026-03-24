using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

public class DriftMonitor : IDriftMonitor
{
    private readonly IInspectionRepository _repository;
    private List<double> _baselineScores = new();
    private readonly object _lock = new();

    public DriftMonitor(IInspectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public bool HasBaseline
    {
        get
        {
            lock (_lock)
            {
                return _baselineScores.Count > 0;
            }
        }
    }

    public void SetBaseline(List<double> baselineScores)
    {
        if (baselineScores == null)
            throw new ArgumentNullException(nameof(baselineScores));
        
        if (baselineScores.Count == 0)
            throw new ArgumentException("Baseline scores cannot be empty.", nameof(baselineScores));

        if (baselineScores.Count < 30)
            throw new ArgumentException("Baseline requires at least 30 samples for reliable analysis.", nameof(baselineScores));

        lock (_lock)
        {
            _baselineScores = new List<double>(baselineScores);
        }
    }

    public async Task<DriftReport> AnalyzeAsync(CancellationToken ct = default)
    {
        List<double> baseline;
        lock (_lock)
        {
            if (_baselineScores.Count == 0)
                throw new InvalidOperationException("No baseline has been set. Call SetBaseline first.");
            
            baseline = new List<double>(_baselineScores);
        }

        var recentScores = await _repository.GetAnomalyScoresAsync(200, ct);
        
        if (recentScores.Count < 10)
        {
            return new DriftReport
            {
                Timestamp = DateTime.UtcNow,
                PValue = 0.0,
                KsStatistic = 0.0,
                IsDriftDetected = false,
                Severity = DriftSeverity.None,
                BaselineSampleCount = baseline.Count,
                CurrentSampleCount = recentScores.Count,
                BaselineStats = CalculateDistributionStats(baseline),
                CurrentStats = CalculateDistributionStats(recentScores),
                Notes = "Insufficient data for drift analysis (less than 10 recent samples)",
                Recommendations = { "Collect more data before performing drift analysis" }
            };
        }

        if (recentScores.Count < 30)
        {
            return new DriftReport
            {
                Timestamp = DateTime.UtcNow,
                PValue = 0.0,
                KsStatistic = 0.0,
                IsDriftDetected = false,
                Severity = DriftSeverity.None,
                BaselineSampleCount = baseline.Count,
                CurrentSampleCount = recentScores.Count,
                BaselineStats = CalculateDistributionStats(baseline),
                CurrentStats = CalculateDistributionStats(recentScores),
                Notes = "Insufficient data for reliable drift analysis (less than 30 recent samples)",
                Recommendations = { "Collect more data for reliable drift detection" }
            };
        }

        var baselineArray = baseline.ToArray();
        var recentArray = recentScores.ToArray();
        
        var ksStatistic = ComputeKsStatistic(baselineArray, recentArray);
        var pValue = ComputePValue(ksStatistic, baselineArray.Length, recentArray.Length);
        
        var (isDriftDetected, severity, recommendations) = InterpretResults(pValue);

        return new DriftReport
        {
            Timestamp = DateTime.UtcNow,
            PValue = pValue,
            KsStatistic = ksStatistic,
            IsDriftDetected = isDriftDetected,
            Severity = severity,
            BaselineSampleCount = baselineArray.Length,
            CurrentSampleCount = recentArray.Length,
            BaselineStats = CalculateDistributionStats(baseline),
            CurrentStats = CalculateDistributionStats(recentScores),
            Recommendations = recommendations
        };
    }

    private double ComputeKsStatistic(double[] sample1, double[] sample2)
    {
        if (sample1.Length == 0 || sample2.Length == 0)
            return 0.0;

        if (sample1.Length == 1 && sample2.Length == 1)
            return sample1[0] == sample2[0] ? 0.0 : 1.0;

        var sorted1 = sample1.OrderBy(x => x).ToArray();
        var sorted2 = sample2.OrderBy(x => x).ToArray();
        
        var allValues = sorted1.Concat(sorted2).Distinct().OrderBy(x => x).ToArray();
        
        double maxDifference = 0.0;
        
        foreach (var value in allValues)
        {
            var cdf1 = CalculateEmpiricalCdf(sorted1, value);
            var cdf2 = CalculateEmpiricalCdf(sorted2, value);
            var difference = Math.Abs(cdf1 - cdf2);
            
            if (difference > maxDifference)
                maxDifference = difference;
        }
        
        return maxDifference;
    }

    private double CalculateEmpiricalCdf(double[] sortedData, double value)
    {
        if (sortedData.Length == 0)
            return 0.0;

        int count = 0;
        foreach (var x in sortedData)
        {
            if (x <= value)
                count++;
            else
                break;
        }
        
        return (double)count / sortedData.Length;
    }

    private double ComputePValue(double ksStatistic, int n1, int n2)
    {
        if (n1 == 0 || n2 == 0)
            return 1.0;

        var effectiveN = (double)(n1 * n2) / (n1 + n2);
        var argument = -2.0 * effectiveN * ksStatistic * ksStatistic;
        
        if (argument < -700)
            return 0.0;
        
        var pValue = 2.0 * Math.Exp(argument);
        return Math.Min(1.0, Math.Max(0.0, pValue));
    }

    private (bool isDriftDetected, DriftSeverity severity, List<string> recommendations) InterpretResults(double pValue)
    {
        var recommendations = new List<string>();
        
        if (pValue >= 0.1)
        {
            recommendations.Add("Model distribution is stable. Continue monitoring.");
            return (false, DriftSeverity.None, recommendations);
        }
        else if (pValue >= 0.05)
        {
            recommendations.Add("Minor drift detected. Monitor closely for trends.");
            recommendations.Add("Consider increasing monitoring frequency.");
            return (false, DriftSeverity.Minor, recommendations);
        }
        else if (pValue >= 0.01)
        {
            recommendations.Add("Moderate drift detected. Monitor closely and investigate potential causes.");
            recommendations.Add("Consider validating model performance on recent data.");
            return (true, DriftSeverity.Moderate, recommendations);
        }
        else
        {
            recommendations.Add("Significant drift detected. Model retraining recommended.");
            recommendations.Add("Investigate root causes of distribution change.");
            recommendations.Add("Consider updating baseline with recent validated data.");
            return (true, DriftSeverity.Significant, recommendations);
        }
    }

    private DistributionStats CalculateDistributionStats(List<double> data)
    {
        if (data.Count == 0)
        {
            return new DistributionStats
            {
                Mean = 0.0,
                StandardDeviation = 0.0,
                Minimum = 0.0,
                Maximum = 0.0,
                Q25 = 0.0,
                Median = 0.0,
                Q75 = 0.0
            };
        }

        var sortedData = data.OrderBy(x => x).ToArray();
        var mean = data.Average();
        var variance = data.Sum(x => Math.Pow(x - mean, 2)) / data.Count;
        var standardDeviation = Math.Sqrt(variance);

        return new DistributionStats
        {
            Mean = mean,
            StandardDeviation = standardDeviation,
            Minimum = sortedData.First(),
            Maximum = sortedData.Last(),
            Q25 = CalculatePercentile(sortedData, 0.25),
            Median = CalculatePercentile(sortedData, 0.5),
            Q75 = CalculatePercentile(sortedData, 0.75)
        };
    }

    private double CalculatePercentile(double[] sortedData, double percentile)
    {
        if (sortedData.Length == 0)
            return 0.0;
        
        if (sortedData.Length == 1)
            return sortedData[0];

        var index = percentile * (sortedData.Length - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedData[lowerIndex];

        var weight = index - lowerIndex;
        return sortedData[lowerIndex] * (1 - weight) + sortedData[upperIndex] * weight;
    }
}