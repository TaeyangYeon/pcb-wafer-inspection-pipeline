using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InspectionPipeline.Core.Data;
using InspectionPipeline.Core.Interfaces;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Services;

/// <summary>
/// Repository implementation for managing inspection records using Entity Framework Core.
/// Follows async-first patterns and never exposes DbContext outside the repository layer.
/// </summary>
public class InspectionRepository : IInspectionRepository
{
    private readonly InspectionDbContext _context;

    /// <summary>
    /// Initializes a new instance of the InspectionRepository class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public InspectionRepository(InspectionDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Saves an inspection record and its associated defect details in a single transaction.
    /// </summary>
    /// <param name="record">The inspection record to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved record with populated ID.</returns>
    public async Task<InspectionRecord> SaveRecordAsync(InspectionRecord record, CancellationToken ct = default)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        try
        {
            await _context.InspectionRecords.AddAsync(record, ct);
            await _context.SaveChangesAsync(ct);
            return record;
        }
        catch (Exception)
        {
            // If save fails, make sure entity is detached to prevent issues with next save
            _context.Entry(record).State = EntityState.Detached;
            throw;
        }
    }

    /// <summary>
    /// Retrieves inspection records with optional date range and result filtering.
    /// </summary>
    /// <param name="from">Optional start date filter.</param>
    /// <param name="to">Optional end date filter.</param>
    /// <param name="resultFilter">Optional result type filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching inspection records with DefectDetails included.</returns>
    public async Task<List<InspectionRecord>> GetRecordsAsync(DateTime? from = null, DateTime? to = null, 
        string? resultFilter = null, CancellationToken ct = default)
    {
        var query = _context.InspectionRecords
            .Include(r => r.DefectDetails)
            .Include(r => r.ModelVersion)
            .AsQueryable();

        // Apply date range filters
        if (from.HasValue)
        {
            query = query.Where(r => r.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(r => r.Timestamp <= to.Value);
        }

        // Apply result filter
        if (!string.IsNullOrWhiteSpace(resultFilter))
        {
            query = query.Where(r => r.FinalResult == resultFilter);
        }

        // Order by timestamp descending (newest first)
        query = query.OrderByDescending(r => r.Timestamp);

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Calculates session statistics including counts, averages, and defect breakdowns.
    /// Returns zeros for empty database.
    /// </summary>
    /// <param name="from">Optional start date for statistics calculation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Session statistics.</returns>
    public async Task<SessionStats> GetStatsAsync(DateTime? from = null, CancellationToken ct = default)
    {
        var query = _context.InspectionRecords.AsQueryable();

        // Apply date filter if provided
        if (from.HasValue)
        {
            query = query.Where(r => r.Timestamp >= from.Value);
        }

        // Get basic counts
        var totalCount = await query.CountAsync(ct);
        
        if (totalCount == 0)
        {
            // Return empty stats for empty database
            var now = DateTime.UtcNow;
            return new SessionStats
            {
                SessionStart = from ?? now,
                SessionEnd = now,
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
        }

        // Calculate aggregations
        var records = await query.ToListAsync(ct);
        
        var okCount = records.Count(r => r.FinalResult == "OK");
        var anomalyCount = records.Count(r => r.FinalResult == "ANOMALY");
        var ngCount = records.Count(r => r.FinalResult == "NG");

        var avgInferenceTime = records.Average(r => r.InferenceTimeMs);
        var minInferenceTime = records.Min(r => r.InferenceTimeMs);
        var maxInferenceTime = records.Max(r => r.InferenceTimeMs);
        var avgAnomalyScore = records.Average(r => r.AnomalyScore);

        // Calculate session time range
        var minTimestamp = records.Min(r => r.Timestamp);
        var maxTimestamp = records.Max(r => r.Timestamp);

        // Calculate defect class breakdown
        var defectClassStats = await CalculateDefectClassStatsAsync(from, ct);

        return new SessionStats
        {
            SessionStart = from ?? minTimestamp,
            SessionEnd = maxTimestamp,
            TotalInspections = totalCount,
            OkCount = okCount,
            AnomalyCount = anomalyCount,
            NgCount = ngCount,
            AverageInferenceTime = avgInferenceTime,
            MinInferenceTime = minInferenceTime,
            MaxInferenceTime = maxInferenceTime,
            AverageAnomalyScore = avgAnomalyScore,
            DefectClassBreakdown = defectClassStats,
            Uptime = maxTimestamp - (from ?? minTimestamp)
        };
    }

    /// <summary>
    /// Retrieves the most recent anomaly scores for drift analysis.
    /// </summary>
    /// <param name="lastN">Number of most recent scores to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of anomaly scores ordered by timestamp descending.</returns>
    public async Task<List<double>> GetAnomalyScoresAsync(int lastN = 200, CancellationToken ct = default)
    {
        return await _context.InspectionRecords
            .OrderByDescending(r => r.Timestamp)
            .Take(lastN)
            .Select(r => r.AnomalyScore)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Exports all inspection records to a CSV file with proper formatting.
    /// Creates parent directory if it doesn't exist.
    /// </summary>
    /// <param name="filePath">Path where CSV file will be saved.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExportToCsvAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        // Create parent directory if it doesn't exist
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Get all records with defect details
        var records = await _context.InspectionRecords
            .Include(r => r.DefectDetails)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(ct);

        var csvContent = new StringBuilder();
        
        // CSV header
        csvContent.AppendLine("Timestamp,ImagePath,FinalResult,StageUsed,AnomalyScore,InferenceTimeMs,DefectCount");

        // CSV rows
        foreach (var record in records)
        {
            var line = $"{record.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                      $"\"{record.ImagePath}\"," +
                      $"{record.FinalResult}," +
                      $"{record.StageUsed}," +
                      $"{record.AnomalyScore:F6}," +
                      $"{record.InferenceTimeMs:F3}," +
                      $"{record.DefectDetails.Count}";
            
            csvContent.AppendLine(line);
        }

        // Write to file
        await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.UTF8, ct);
    }

    /// <summary>
    /// Calculates defect class statistics for the session stats.
    /// </summary>
    /// <param name="from">Optional start date filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of defect class statistics.</returns>
    private async Task<List<DefectClassStats>> CalculateDefectClassStatsAsync(DateTime? from, CancellationToken ct)
    {
        var defectQuery = _context.DefectDetails
            .Include(d => d.InspectionRecord)
            .AsQueryable();

        // Apply date filter if provided
        if (from.HasValue)
        {
            defectQuery = defectQuery.Where(d => d.InspectionRecord.Timestamp >= from.Value);
        }

        var defects = await defectQuery.ToListAsync(ct);
        
        if (!defects.Any())
            return new List<DefectClassStats>();

        var totalDefects = defects.Count;

        var classStats = defects
            .GroupBy(d => d.ClassName)
            .Select(g => new DefectClassStats
            {
                ClassName = g.Key,
                Count = g.Count(),
                AverageConfidence = g.Average(d => d.Confidence),
                Percentage = (double)g.Count() / totalDefects * 100.0
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        return classStats;
    }
}