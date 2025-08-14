namespace Api.Orders.Models;

public sealed record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    DateTime CreatedAtUtc
);
