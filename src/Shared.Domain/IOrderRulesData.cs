namespace Shared.Domain.Orders;

/// <summary>
/// Abstraction for data access needed by order validation rules.
/// This interface is implemented differently by each host:
/// - Dataverse plugin uses IOrganizationService
/// - ASP.NET Core API uses Dataverse SDK/Web API or cached repositories
/// </summary>
public interface IOrderRulesData
{
    /// <summary>
    /// Checks if a customer exists by ID
    /// </summary>
    /// <param name="customerId">Customer ID (GUID as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if customer exists, false otherwise</returns>
    Task<bool> CustomerExistsAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a product exists by ID
    /// </summary>
    /// <param name="productId">Product ID (GUID as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if product exists, false otherwise</returns>
    Task<bool> ProductExistsAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the catalog price for a product
    /// </summary>
    /// <param name="productId">Product ID (GUID as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product price if found, null if product doesn't exist</returns>
    Task<decimal?> TryGetProductPriceAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an order number is unique (not already used)
    /// </summary>
    /// <param name="orderNumber">Order number to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if order number is unique, false if already used</returns>
    Task<bool> IsOrderNumberUniqueAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets customer details needed for validation
    /// </summary>
    /// <param name="customerId">Customer ID (GUID as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer info if found, null if not found</returns>
    Task<CustomerInfo?> TryGetCustomerInfoAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product details needed for validation
    /// </summary>
    /// <param name="productId">Product ID (GUID as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product info if found, null if not found</returns>
    Task<ProductInfo?> TryGetProductInfoAsync(string productId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Customer information needed for validation
/// </summary>
public sealed record CustomerInfo(
    string Id,
    string Name,
    bool IsActive,
    decimal CreditLimit);

/// <summary>
/// Product information needed for validation
/// </summary>
public sealed record ProductInfo(
    string Id,
    string Name,
    bool IsActive,
    decimal Price,
    int StockQuantity);