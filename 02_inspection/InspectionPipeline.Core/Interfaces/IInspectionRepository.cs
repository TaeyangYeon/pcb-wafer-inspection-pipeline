using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Repository for managing inspection records in the SQLite database using Entity Framework Core.
/// </summary>
public interface IInspectionRepository
{
    /// <summary>
    /// Saves an inspection record to the database.
    /// </summary>
    /// <param name="record">The inspection record to save.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the saved record with populated ID.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
    Task<InspectionRecord> SaveRecordAsync(InspectionRecord record, CancellationToken ct = default);

    /// <summary>
    /// Retrieves inspection records from the database with optional filtering.
    /// </summary>
    /// <param name="from">Optional start date filter. If null, includes records from the beginning.</param>
    /// <param name="to">Optional end date filter. If null, includes records until the end.</param>
    /// <param name="resultFilter">Optional result filter (e.g., "PASS", "FAIL"). If null, includes all results.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of matching inspection records.</returns>
    Task<List<InspectionRecord>> GetRecordsAsync(DateTime? from = null, DateTime? to = null, string? resultFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Calculates and retrieves session statistics from the database.
    /// </summary>
    /// <param name="from">Optional start date for statistics calculation. If null, calculates from all records.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing session statistics.</returns>
    Task<SessionStats> GetStatsAsync(DateTime? from = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the most recent anomaly scores for drift analysis.
    /// </summary>
    /// <param name="lastN">The number of most recent scores to retrieve. Default is 200.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of anomaly scores ordered by timestamp (newest first).</returns>
    Task<List<double>> GetAnomalyScoresAsync(int lastN = 200, CancellationToken ct = default);

    /// <summary>
    /// Exports inspection records to a CSV file.
    /// </summary>
    /// <param name="filePath">The file path where the CSV file will be saved.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null or empty.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the application lacks permission to write to the specified path.</exception>
    Task ExportToCsvAsync(string filePath, CancellationToken ct = default);
}