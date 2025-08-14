using FluentValidation;
using FluentValidation.Results;

namespace Shared.Domain.Common;

/// <summary>
/// Extension methods for working with FluentValidation results
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Converts FluentValidation errors to a dictionary for API responses
    /// </summary>
    /// <param name="result">Validation result</param>
    /// <returns>Dictionary with property names as keys and error messages as values</returns>
    public static Dictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());
    }

    /// <summary>
    /// Gets all error messages as a single concatenated string
    /// </summary>
    /// <param name="result">Validation result</param>
    /// <param name="separator">Separator between error messages</param>
    /// <returns>Concatenated error messages</returns>
    public static string GetErrorsAsString(this ValidationResult result, string separator = "; ")
    {
        return string.Join(separator, result.Errors.Select(e => e.ErrorMessage));
    }

    /// <summary>
    /// Gets all error codes from the validation result
    /// </summary>
    /// <param name="result">Validation result</param>
    /// <returns>List of error codes</returns>
    public static IReadOnlyList<string> GetErrorCodes(this ValidationResult result)
    {
        return result.Errors
            .Select(e => e.ErrorCode)
            .Where(code => !string.IsNullOrEmpty(code))
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Creates a validation context with common root context data
    /// </summary>
    /// <typeparam name="T">Type being validated</typeparam>
    /// <param name="instance">Instance to validate</param>
    /// <param name="rulesData">Rules data for validation</param>
    /// <returns>ValidationContext with rules data in root context</returns>
    public static ValidationContext<T> CreateContextWithRulesData<T>(T instance, object rulesData)
    {
        var context = new ValidationContext<T>(instance);
        context.RootContextData["rulesData"] = rulesData;
        return context;
    }

    /// <summary>
    /// Validates a GUID string format
    /// </summary>
    /// <param name="guidString">String to validate</param>
    /// <returns>True if valid GUID format, false otherwise</returns>
    public static bool IsValidGuid(this string? guidString)
    {
        return !string.IsNullOrEmpty(guidString) && Guid.TryParse(guidString, out _);
    }

    /// <summary>
    /// Safely converts a string to Guid
    /// </summary>
    /// <param name="guidString">String to convert</param>
    /// <returns>Guid if valid, null if invalid</returns>
    public static Guid? ToGuid(this string? guidString)
    {
        return Guid.TryParse(guidString, out var guid) ? guid : null;
    }
}