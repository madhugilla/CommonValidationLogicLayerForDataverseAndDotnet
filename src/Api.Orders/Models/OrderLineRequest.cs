using Shared.Domain.Orders;

namespace Api.Orders.Models;

public sealed record OrderLineRequest(
    string ProductId,
    int Quantity,
    decimal UnitPrice
)
{
    public OrderLineCommand ToCommand() => new(ProductId, Quantity, UnitPrice);
}
