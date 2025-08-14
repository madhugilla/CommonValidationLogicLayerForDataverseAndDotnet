using Api.Orders.Models;
using Api.Orders.Services;
using FluentValidation;
using FluentValidationException = FluentValidation.ValidationException;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace Api.Orders.Controllers;

/// <summary>
/// Controller for order management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new order with validation
    /// </summary>
    /// <param name="request">Order creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created order details</returns>
    /// <response code="201">Order created successfully</response>
    /// <response code="400">Invalid request or validation failed</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ApiErrorResponse), 500)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating order via API: {OrderNumber}", request.OrderNumber);

        try
        {
            // Basic model validation is handled by ASP.NET Core model binding
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state validation failed for order: {OrderNumber}", request.OrderNumber);
                return ValidationProblem(ModelState);
            }

            // Convert request to domain command
            var command = request.ToCommand();

            // The shared validation logic will run inside the service
            var response = await _orderService.CreateOrderAsync(command, cancellationToken);

            _logger.LogInformation("Order created successfully via API: {OrderNumber} -> {OrderId}", 
                request.OrderNumber, response.OrderId);

            return CreatedAtAction(
                actionName: nameof(GetOrderById),
                routeValues: new { id = response.OrderId },
                value: response);
        }
    catch (FluentValidationException validationEx)
        {
            _logger.LogWarning("Order validation failed: {OrderNumber} - {Errors}", 
                request.OrderNumber, string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage)));

            // Convert FluentValidation errors to ASP.NET Core ValidationProblemDetails
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Order validation failed",
                Status = 400,
                Detail = "One or more validation errors occurred while creating the order."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order via API: {OrderNumber}", request.OrderNumber);
            
            return StatusCode(500, new ApiErrorResponse
            {
                Type = "ServerError",
                Title = "Internal server error",
                Status = 500,
                Detail = "An unexpected error occurred while creating the order.",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Gets order details by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    /// <response code="200">Order found</response>
    /// <response code="404">Order not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailsResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(ApiErrorResponse), 500)]
    public async Task<IActionResult> GetOrderById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting order by ID: {OrderId}", id);

        try
        {
            var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);

            if (order == null)
            {
                _logger.LogDebug("Order not found: {OrderId}", id);
                return NotFound();
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by ID: {OrderId}", id);
            
            return StatusCode(500, new ApiErrorResponse
            {
                Type = "ServerError",
                Title = "Internal server error",
                Status = 500,
                Detail = "An unexpected error occurred while retrieving the order.",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Gets order details by order number
    /// </summary>
    /// <param name="orderNumber">Order number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    /// <response code="200">Order found</response>
    /// <response code="404">Order not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("by-number/{orderNumber}")]
    [ProducesResponseType(typeof(OrderDetailsResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(ApiErrorResponse), 500)]
    public async Task<IActionResult> GetOrderByNumber(
        [FromRoute] string orderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting order by number: {OrderNumber}", orderNumber);

        try
        {
            if (string.IsNullOrEmpty(orderNumber))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Type = "ValidationError",
                    Title = "Invalid order number",
                    Status = 400,
                    Detail = "Order number cannot be empty."
                });
            }

            var order = await _orderService.GetOrderByNumberAsync(orderNumber, cancellationToken);

            if (order == null)
            {
                _logger.LogDebug("Order not found: {OrderNumber}", orderNumber);
                return NotFound();
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by number: {OrderNumber}", orderNumber);
            
            return StatusCode(500, new ApiErrorResponse
            {
                Type = "ServerError",
                Title = "Internal server error",
                Status = 500,
                Detail = "An unexpected error occurred while retrieving the order.",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Gets orders for a customer
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="pageSize">Number of orders per page (default: 50, max: 100)</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged list of orders</returns>
    /// <response code="200">Orders retrieved successfully</response>
    /// <response code="400">Invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(PagedResult<OrderDetailsResponse>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 400)]
    [ProducesResponseType(typeof(ApiErrorResponse), 500)]
    public async Task<IActionResult> GetOrdersByCustomer(
        [FromRoute] Guid customerId,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting orders for customer: {CustomerId}, Page: {PageNumber}, Size: {PageSize}", 
            customerId, pageNumber, pageSize);

        try
        {
            var orders = await _orderService.GetOrdersByCustomerAsync(
                customerId.ToString(), pageSize, pageNumber, cancellationToken);

            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders for customer: {CustomerId}", customerId);
            
            return StatusCode(500, new ApiErrorResponse
            {
                Type = "ServerError",
                Title = "Internal server error",
                Status = 500,
                Detail = "An unexpected error occurred while retrieving customer orders.",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Validates an order without creating it
    /// </summary>
    /// <param name="request">Order to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    /// <response code="200">Validation completed</response>
    /// <response code="400">Invalid request</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(OrderValidationResult), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ApiErrorResponse), 500)]
    public async Task<IActionResult> ValidateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating order via API: {OrderNumber}", request.OrderNumber);

        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var command = request.ToCommand();
            var validationResult = await _orderService.ValidateOrderAsync(command, cancellationToken);

            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating order via API: {OrderNumber}", request.OrderNumber);
            
            return StatusCode(500, new ApiErrorResponse
            {
                Type = "ServerError",
                Title = "Internal server error",
                Status = 500,
                Detail = "An unexpected error occurred while validating the order.",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Gets API health status
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Orders API",
            Version = "1.0.0"
        });
    }
}