using Api.Orders.Models;
using Shared.Domain.Orders;

namespace Api.Orders.Services;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
    Task<OrderDetailsResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderDetailsResponse?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDetailsResponse>> GetOrdersByCustomerAsync(string customerId, int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<OrderValidationResult> ValidateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
}
