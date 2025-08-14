namespace Api.Orders.Models;

public sealed record OrderLineResponse(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public sealed record OrderDetailsResponse(
    Guid OrderId,
    string OrderNumber,
    string CustomerId,
    DateTime OrderDate,
    decimal TotalAmount,
    IReadOnlyList<OrderLineResponse> Lines
);
