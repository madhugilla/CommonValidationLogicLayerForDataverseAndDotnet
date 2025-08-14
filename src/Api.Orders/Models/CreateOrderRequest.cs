using System.ComponentModel.DataAnnotations;
using Shared.Domain.Orders;

namespace Api.Orders.Models;

/// <summary>
/// Request model for creating a new order via the API
/// </summary>
public sealed class CreateOrderRequest
{
    /// <summary>
    /// Customer ID (GUID)
    /// </summary>
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Order date (defaults to today if not provided)
    /// </summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>
    /// Unique order number
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Total order amount
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than zero")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Order line items
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one order line is required")]
    public List<OrderLineRequest> Lines { get; set; } = new();

    /// <summary>
    /// Converts this request to a domain command
    /// </summary>
    public CreateOrderCommand ToCommand()
    {
        return new CreateOrderCommand(
            CustomerId: CustomerId,
            OrderDate: OrderDate ?? DateTime.Today,
            OrderNumber: OrderNumber,
            TotalAmount: TotalAmount,
            Lines: Lines.Select(l => l.ToCommand()).ToList().AsReadOnly()
        );
    }
}

// ...existing code for OrderLineRequest...
