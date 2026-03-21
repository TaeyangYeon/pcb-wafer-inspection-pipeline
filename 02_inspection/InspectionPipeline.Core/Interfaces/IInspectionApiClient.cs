using System.Threading;
using System.Threading.Tasks;
using InspectionPipeline.Core.Models;

namespace InspectionPipeline.Core.Interfaces;

/// <summary>
/// Client for communicating with the FastAPI inspection server.
/// </summary>
public interface IInspectionApiClient
{
    /// <summary>
    /// Sends an image to the FastAPI server for inspection and returns the analysis results.
    /// </summary>
    /// <param name="imagePath">The full file path of the image to inspect.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the inspection results.</returns>
    /// <exception cref="ArgumentException">Thrown when the image path is null, empty, or the file does not exist.</exception>
    /// <exception cref="HttpRequestException">Thrown when the API request fails due to network or server issues.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<PipelineResult> InspectAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// Checks if the FastAPI server is healthy and responsive.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing true if the server is healthy, false otherwise.</returns>
    /// <remarks>
    /// This method should not throw exceptions for typical network failures.
    /// Returns false if the server is unreachable or returns an error status.
    /// </remarks>
    Task<bool> IsServerHealthyAsync(CancellationToken ct = default);
}