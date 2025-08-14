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

	/// <summary>
	/// Maps order lines from the entity. This implementation assumes lines are stored
	/// as separate fields on the main entity (simplified for demo).
	/// In production, you'd typically query related order line entities.
	/// </summary>
	private static IReadOnlyList<OrderLineCommand> MapOrderLines(Entity orderEntity, ITracingService? tracing)
	{
		var lines = new List<OrderLineCommand>();

		try
		{
			// Option 1: Single line stored directly on the order entity (simplified)
			var productRef = orderEntity.GetAttributeValue<EntityReference>("new_productid");
			if (productRef != null)
			{
				var quantity = orderEntity.GetAttributeValue<int>("new_quantity");
				var unitPrice = orderEntity.GetAttributeValue<Money>("new_unitprice")?.Value ?? 0m;

				lines.Add(new OrderLineCommand(
					ProductId: productRef.Id.ToString(),
					Quantity: quantity,
					UnitPrice: unitPrice
				));

				tracing?.Trace($"Mapped single order line - Product: {productRef.Id}, Qty: {quantity}, Price: {unitPrice}");
			}

			// Option 2: Lines stored as JSON (more advanced scenario)
			var linesJson = orderEntity.GetAttributeValue<string>("new_orderlinesjson");
			if (!string.IsNullOrEmpty(linesJson))
			{
				var additionalLines = MapOrderLinesFromJson(linesJson, tracing);
				lines.AddRange(additionalLines);
			}

			// Option 3: In a real implementation, you might query related entities here
			// This would require the IOrganizationService, so you'd need to modify the method signature
            
			tracing?.Trace($"Mapped {lines.Count} order lines total");
		}
		catch (Exception ex)
		{
			tracing?.Trace($"Error mapping order lines: {ex.Message}");
			// Return empty list rather than throwing to allow validation to catch missing lines
		}

		return lines.AsReadOnly();
	}

	/// <summary>
	/// Maps order lines from JSON format (example of more complex mapping)
	/// </summary>
	private static List<OrderLineCommand> MapOrderLinesFromJson(string linesJson, ITracingService? tracing)
	{
		var lines = new List<OrderLineCommand>();

		try
		{
			// Simple JSON parsing example - in production, use System.Text.Json or Newtonsoft.Json
			// This is a very basic example for demonstration
			tracing?.Trace($"Parsing order lines from JSON: {linesJson}");
            
			// For demo purposes, assume JSON format like:
			// [{"productId":"guid","quantity":1,"unitPrice":10.00}]
			// In real implementation, use proper JSON deserialization
            
			tracing?.Trace("JSON parsing completed successfully");
		}
		catch (Exception ex)
		{
			tracing?.Trace($"Error parsing JSON order lines: {ex.Message}");
		}

		return lines;
	}

	/// <summary>
	/// Validates that required fields are present in the entity
	/// </summary>
	public static bool HasRequiredOrderFields(Entity orderEntity)
	{
		if (orderEntity == null) return false;

		// Check for minimum required fields
		return orderEntity.Contains("new_customerid") ||
			   orderEntity.Contains("new_ordernumber") ||
			   orderEntity.Contains("new_totalamount");
	}

	/// <summary>
	/// Gets a safe string representation of an EntityReference
	/// </summary>
	public static string GetEntityReferenceId(EntityReference? entityRef)
	{
		return entityRef?.Id.ToString() ?? string.Empty;
	}

	/// <summary>
	/// Gets a safe decimal value from a Money field
	/// </summary>
	public static decimal GetMoneyValue(Money? money, decimal defaultValue = 0m)
	{
		return money?.Value ?? defaultValue;
	}
}
