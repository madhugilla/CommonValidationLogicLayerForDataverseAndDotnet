// Moved to src/Api.Orders/Models/CreateOrderRequest.cs
// (Root stub to avoid duplicate compilation.)
/// Standard API error response
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>
    /// Error type/code
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Detailed error description
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Validation errors by field
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Trace ID for tracking
    /// </summary>
    public string? TraceId { get; set; }
}