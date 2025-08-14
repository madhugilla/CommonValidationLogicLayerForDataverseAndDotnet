using Microsoft.Xrm.Sdk;
using Shared.Domain.Orders;

namespace Plugins.Dataverse.Mapping;

/// <summary>
/// Maps Dataverse Entity objects to domain command objects
/// </summary>
public static class EntityMapper
{
    /// <summary>
    /// Maps a Dataverse order entity to CreateOrderCommand
    /// Adjust field names based on your actual Dataverse schema
    /// </summary>
    /// <param name="orderEntity">Order entity from Dataverse</param>
    /// <param name="tracing">Optional tracing service for debugging</param>
    /// <returns>CreateOrderCommand mapped from the entity</returns>
    public static CreateOrderCommand MapToCreateOrderCommand(Entity orderEntity, ITracingService? tracing = null)
    {
        if (orderEntity == null)
            throw new ArgumentNullException(nameof(orderEntity));

        tracing?.Trace("Mapping entity to CreateOrderCommand");

        try
        {
            // Extract customer ID from EntityReference
            var customerRef = orderEntity.GetAttributeValue<EntityReference>("new_customerid");
            var customerId = customerRef?.Id.ToString() ?? string.Empty;
            
            // Get order date (default to today if not provided)
            var orderDate = orderEntity.GetAttributeValue<DateTime?>("new_orderdate") ?? DateTime.Today;
            
            // Get order number
            var orderNumber = orderEntity.GetAttributeValue<string>("new_ordernumber") ?? string.Empty;
            
            // Get total amount
            var totalAmount = orderEntity.GetAttributeValue<Money>("new_totalamount")?.Value ?? 0m;

            tracing?.Trace($"Mapped basic fields - Customer: {customerId}, OrderNumber: {orderNumber}, Total: {totalAmount}");

            // Map order lines - this is a simplified example where order lines are stored as JSON
            // In a real scenario, you might query related entities or use a different approach
            var orderLines = MapOrderLines(orderEntity, tracing);

            var command = new CreateOrderCommand(
                CustomerId: customerId,
                OrderDate: orderDate,
                OrderNumber: orderNumber,
                TotalAmount: totalAmount,
                Lines: orderLines
            );

            tracing?.Trace($"Successfully mapped to CreateOrderCommand with {orderLines.Count} lines");
            return command;
        }
        catch (Exception ex)
        {
            tracing?.Trace($"Error mapping entity to command: {ex.Message}");
            throw new InvalidOperationException("Failed to map entity to CreateOrderCommand", ex);
        }
    }

    // ...existing code for MapOrderLines...

    /// <summary>
    /// Checks the entity for required order fields before mapping.
    /// </summary>
    public static bool HasRequiredOrderFields(Entity entity)
    {
        if (entity == null) return false;
        return entity.Contains("new_customerid") &&
               entity.Contains("new_orderdate") &&
               entity.Contains("new_ordernumber") &&
               entity.Contains("new_totalamount");
    }

    /// <summary>
    /// Placeholder implementation for mapping order lines. Adjust to your schema.
    /// </summary>
    public static List<OrderLineCommand> MapOrderLines(Entity orderEntity, ITracingService? tracing)
    {
        // If you store JSON lines in a field like new_orderlinesjson, parse it here.
        // For now return empty list to allow validation pipeline to run.
        tracing?.Trace("No order lines mapping implemented; returning empty list");
        return new List<OrderLineCommand>();
    }
}
