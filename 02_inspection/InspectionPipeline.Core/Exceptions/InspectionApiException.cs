using System;

namespace InspectionPipeline.Core.Exceptions;

/// <summary>
/// Exception thrown when the inspection API client encounters an error during communication
/// with the FastAPI inference server.
/// </summary>
public class InspectionApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the InspectionApiException class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public InspectionApiException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}