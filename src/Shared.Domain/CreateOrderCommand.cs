namespace Shared.Domain.Orders;

/// <summary>
/// Command to create a new order - this is the common model used by both 
/// the Dataverse plugin and ASP.NET Core API for validation
/// </summary>
public sealed record CreateOrderCommand(
    string CustomerId,
    DateTime OrderDate,
    string OrderNumber,
    decimal TotalAmount,
    IReadOnlyList<OrderLineCommand> Lines);

/// <summary>
/// Represents an order line item
/// </summary>
public sealed record OrderLineCommand(
    string ProductId,
    int Quantity,
    decimal UnitPrice)
{
    /// <summary>
    /// Calculated line total
    /// </summary>
    public decimal LineTotal => Quantity * UnitPrice;
};

/// <summary>
/// Common validation error codes
/// </summary>
public static class OrderValidationErrors
{
    public const string CustomerRequired = "CUSTOMER_REQUIRED";
    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";
    public const string OrderDateInvalid = "ORDER_DATE_INVALID";
    public const string OrderNumberRequired = "ORDER_NUMBER_REQUIRED";
    public const string OrderNumberNotUnique = "ORDER_NUMBER_NOT_UNIQUE";
    public const string LinesRequired = "LINES_REQUIRED";
    public const string ProductRequired = "PRODUCT_REQUIRED";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string QuantityInvalid = "QUANTITY_INVALID";
    public const string UnitPriceInvalid = "UNIT_PRICE_INVALID";
    public const string UnitPriceTooLow = "UNIT_PRICE_TOO_LOW";
    public const string TotalAmountMismatch = "TOTAL_AMOUNT_MISMATCH";
}
