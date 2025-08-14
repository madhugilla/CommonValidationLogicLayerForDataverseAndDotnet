namespace Api.Orders.Models;

public sealed record ValidationError(string Code, string Message, string? Field = null);

public sealed record OrderValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors
)
{
    public static OrderValidationResult Success() => new(true, Array.Empty<ValidationError>());
    public static OrderValidationResult Failure(IEnumerable<ValidationError> errors) => new(false, errors.ToList());
}
