using Api.Orders.Models;
using FluentValidation;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Shared.Domain.Orders;
using Shared.Domain.Common;

namespace Api.Orders.Services;

/// <summary>
/// Dataverse implementation of the order service
/// </summary>
public sealed class DataverseOrderService : IOrderService
{
    private readonly ServiceClient _serviceClient;
    private readonly IValidator<CreateOrderCommand> _validator;
    private readonly ILogger<DataverseOrderService> _logger;

    public DataverseOrderService(
        ServiceClient serviceClient,
        IValidator<CreateOrderCommand> validator,
        ILogger<DataverseOrderService> logger)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating order: {OrderNumber}", command.OrderNumber);

        try
        {
            // Validate the order first
            var validationResult = await _validator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.GetErrorsAsString();
                _logger.LogWarning("Order validation failed: {OrderNumber} - {Errors}", command.OrderNumber, errors);
                throw new ValidationException("Order validation failed", validationResult.Errors);
            }

            // Create the order entity
            var orderEntity = new Entity("new_order"); // Adjust entity name as needed
            
            // Map command to entity attributes
            orderEntity["new_customerid"] = new EntityReference("account", Guid.Parse(command.CustomerId));
            orderEntity["new_orderdate"] = command.OrderDate;
            orderEntity["new_ordernumber"] = command.OrderNumber;
            orderEntity["new_totalamount"] = new Money(command.TotalAmount);

            // Add first line item directly to order (simplified approach)
            if (command.Lines.Any())
            {
                var firstLine = command.Lines.First();
                orderEntity["new_productid"] = new EntityReference("product", Guid.Parse(firstLine.ProductId));
                orderEntity["new_quantity"] = firstLine.Quantity;
                orderEntity["new_unitprice"] = new Money(firstLine.UnitPrice);
            }

            // Persist entity
            var id = await _serviceClient.CreateAsync(orderEntity);

            _logger.LogInformation("Order created in Dataverse: {OrderId}", id);

            return new CreateOrderResponse(
                OrderId: id,
                OrderNumber: command.OrderNumber,
                CreatedAtUtc: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order: {OrderNumber}", command.OrderNumber);
            throw;
        }
    }

    public async Task<OrderDetailsResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving order by id {OrderId}", orderId);
        var columns = new ColumnSet("new_ordernumber", "new_customerid", "new_orderdate", "new_totalamount", "new_productid", "new_quantity", "new_unitprice");
        var entity = await _serviceClient.RetrieveAsync("new_order", orderId, columns);
        if (entity == null) return null;
        return MapToDetails(entity);
    }

    public async Task<OrderDetailsResponse?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return null;
        _logger.LogDebug("Retrieving order by number {OrderNumber}", orderNumber);

        var query = new QueryExpression("new_order")
        {
            ColumnSet = new ColumnSet("new_ordernumber", "new_customerid", "new_orderdate", "new_totalamount", "new_productid", "new_quantity", "new_unitprice")
        };
        query.Criteria.AddCondition("new_ordernumber", ConditionOperator.Equal, orderNumber);

        var results = await _serviceClient.RetrieveMultipleAsync(query);
        var entity = results.Entities.FirstOrDefault();
        return entity == null ? null : MapToDetails(entity);
    }

    public async Task<PagedResult<OrderDetailsResponse>> GetOrdersByCustomerAsync(string customerId, int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return PagedResult<OrderDetailsResponse>.Empty(pageNumber, pageSize);
        if (pageSize <= 0) pageSize = 50;
        if (pageNumber <= 0) pageNumber = 1;

        _logger.LogDebug("Retrieving orders for customer {CustomerId} page {Page}", customerId, pageNumber);

        var query = new QueryExpression("new_order")
        {
            ColumnSet = new ColumnSet("new_ordernumber", "new_customerid", "new_orderdate", "new_totalamount", "new_productid", "new_quantity", "new_unitprice"),
            PageInfo = new PagingInfo
            {
                PageNumber = pageNumber,
                Count = pageSize,
                ReturnTotalRecordCount = true
            }
        };
        query.Criteria.AddCondition("new_customerid", ConditionOperator.Equal, customerId);

        var response = await _serviceClient.RetrieveMultipleAsync(query);
        var items = response.Entities.Select(MapToDetails).ToList();
        var total = response.TotalRecordCount >= 0 ? response.TotalRecordCount : items.Count;
        return new PagedResult<OrderDetailsResponse>(pageNumber, pageSize, total, items);
    }

    public async Task<OrderValidationResult> ValidateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _validator.ValidateAsync(command, cancellationToken);
        if (result.IsValid) return OrderValidationResult.Success();
        var errors = result.Errors.Select(e => new ValidationError(e.ErrorCode ?? string.Empty, e.ErrorMessage, e.PropertyName)).ToList();
        return OrderValidationResult.Failure(errors);
    }

    private static OrderDetailsResponse MapToDetails(Entity entity)
    {
        var orderId = entity.Id;
        var orderNumber = entity.GetAttributeValue<string>("new_ordernumber") ?? string.Empty;
        var customerRef = entity.GetAttributeValue<EntityReference>("new_customerid");
        var customerId = customerRef?.Id.ToString() ?? string.Empty;
        var orderDate = entity.GetAttributeValue<DateTime?>("new_orderdate") ?? DateTime.MinValue;
        var totalAmount = entity.GetAttributeValue<Money>("new_totalamount")?.Value ?? 0m;

        // Simplified single line mapping (since we stored first line directly)
        var productRef = entity.GetAttributeValue<EntityReference>("new_productid");
        var quantity = entity.Contains("new_quantity") ? (int)entity["new_quantity"] : 0;
        var unitPrice = entity.GetAttributeValue<Money>("new_unitprice")?.Value ?? 0m;
        var lines = new List<OrderLineResponse>();
        if (productRef != null)
        {
            lines.Add(new OrderLineResponse(
                ProductId: productRef.Id.ToString(),
                ProductName: productRef.Name ?? string.Empty,
                Quantity: quantity,
                UnitPrice: unitPrice,
                LineTotal: quantity * unitPrice));
        }

        return new OrderDetailsResponse(orderId, orderNumber, customerId, orderDate, totalAmount, lines);
    }
}
