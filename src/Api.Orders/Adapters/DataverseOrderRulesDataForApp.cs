using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using System.ServiceModel;
using Shared.Domain.Orders;

namespace Api.Orders.Adapters;

/// <summary>
/// ASP.NET Core implementation of IOrderRulesData using Dataverse ServiceClient.
/// This provides data access for validation rules in the API context.
/// </summary>
public sealed class DataverseOrderRulesDataForApp : IOrderRulesData
{
    private readonly ServiceClient _serviceClient;
    private readonly ILogger<DataverseOrderRulesDataForApp> _logger;

    public DataverseOrderRulesDataForApp(ServiceClient serviceClient, ILogger<DataverseOrderRulesDataForApp> logger)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CustomerExistsAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId) || !Guid.TryParse(customerId, out var customerGuid))
            {
                _logger.LogDebug("Invalid customer ID format: {CustomerId}", customerId);
                return false;
            }

            _logger.LogDebug("Checking if customer exists: {CustomerId}", customerId);

            // Use async retrieve with minimal columns for existence check
            var customer = await _serviceClient.RetrieveAsync(
                entityName: "account",
                id: customerGuid,
                columnSet: new ColumnSet(false), // No columns needed for existence check
                cancellationToken: cancellationToken);

            var exists = customer != null;
            _logger.LogDebug("Customer existence check: {CustomerId} = {Exists}", customerId, exists);
            return exists;
        }
        catch (Exception ex) when (IsEntityNotFoundError(ex))
        {
            _logger.LogDebug("Customer not found: {CustomerId}", customerId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking customer existence: {CustomerId}", customerId);
            throw new InvalidOperationException($"Failed to check customer existence: {customerId}", ex);
        }
    }

    public async Task<bool> ProductExistsAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
            {
                _logger.LogDebug("Invalid product ID format: {ProductId}", productId);
                return false;
            }

            _logger.LogDebug("Checking if product exists: {ProductId}", productId);

            var product = await _serviceClient.RetrieveAsync(
                entityName: "product",
                id: productGuid,
                columnSet: new ColumnSet(false),
                cancellationToken: cancellationToken);

            var exists = product != null;
            _logger.LogDebug("Product existence check: {ProductId} = {Exists}", productId, exists);
            return exists;
        }
        catch (Exception ex) when (IsEntityNotFoundError(ex))
        {
            _logger.LogDebug("Product not found: {ProductId}", productId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking product existence: {ProductId}", productId);
            throw new InvalidOperationException($"Failed to check product existence: {productId}", ex);
        }
    }

    public async Task<decimal?> TryGetProductPriceAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
            {
                _logger.LogDebug("Invalid product ID format for price lookup: {ProductId}", productId);
                return null;
            }

            _logger.LogDebug("Getting product price: {ProductId}", productId);

            var product = await _serviceClient.RetrieveAsync(
                entityName: "product",
                id: productGuid,
                columnSet: new ColumnSet("price"),
                cancellationToken: cancellationToken);

            var price = product?.GetAttributeValue<Money>("price")?.Value;
            _logger.LogDebug("Product price retrieved: {ProductId} = {Price}", productId, price);
            return price;
        }
        catch (Exception ex) when (IsEntityNotFoundError(ex))
        {
            _logger.LogDebug("Product not found for price lookup: {ProductId}", productId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product price: {ProductId}", productId);
            throw new InvalidOperationException($"Failed to get product price: {productId}", ex);
        }
    }

    public async Task<bool> IsOrderNumberUniqueAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(orderNumber))
            {
                _logger.LogDebug("Empty order number provided for uniqueness check");
                return false;
            }

            _logger.LogDebug("Checking order number uniqueness: {OrderNumber}", orderNumber);

            var query = new QueryExpression("new_order") // Adjust entity name as needed
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("new_ordernumber", ConditionOperator.Equal, orderNumber)
                    }
                }
            };

            var results = await _serviceClient.RetrieveMultipleAsync(query, cancellationToken);
            var isUnique = !results.Entities.Any();
            
            _logger.LogDebug("Order number uniqueness check: {OrderNumber} = {IsUnique}", orderNumber, isUnique);
            return isUnique;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking order number uniqueness: {OrderNumber}", orderNumber);
            throw new InvalidOperationException($"Failed to check order number uniqueness: {orderNumber}", ex);
        }
    }

    public async Task<CustomerInfo?> TryGetCustomerInfoAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId) || !Guid.TryParse(customerId, out var customerGuid))
            {
                _logger.LogDebug("Invalid customer ID format for info lookup: {CustomerId}", customerId);
                return null;
            }

            _logger.LogDebug("Getting customer info: {CustomerId}", customerId);

            var customer = await _serviceClient.RetrieveAsync(
                entityName: "account",
                id: customerGuid,
                columnSet: new ColumnSet("name", "statecode", "creditlimit"),
                cancellationToken: cancellationToken);

            if (customer == null)
            {
                _logger.LogDebug("Customer not found for info lookup: {CustomerId}", customerId);
                return null;
            }

            var customerInfo = new CustomerInfo(
                Id: customerId,
                Name: customer.GetAttributeValue<string>("name") ?? "",
                IsActive: customer.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0, // Active = 0
                CreditLimit: customer.GetAttributeValue<Money>("creditlimit")?.Value ?? 0m
            );

            _logger.LogDebug("Customer info retrieved: {CustomerId} - {Name} (Active: {IsActive})", 
                customerId, customerInfo.Name, customerInfo.IsActive);
            return customerInfo;
        }
        catch (Exception ex) when (IsEntityNotFoundError(ex))
        {
            _logger.LogDebug("Customer not found for info lookup: {CustomerId}", customerId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer info: {CustomerId}", customerId);
            throw new InvalidOperationException($"Failed to get customer info: {customerId}", ex);
        }
    }

    public async Task<ProductInfo?> TryGetProductInfoAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
            {
                _logger.LogDebug("Invalid product ID format for info lookup: {ProductId}", productId);
                return null;
            }

            _logger.LogDebug("Getting product info: {ProductId}", productId);

            var product = await _serviceClient.RetrieveAsync(
                entityName: "product",
                id: productGuid,
                columnSet: new ColumnSet("name", "statecode", "price", "quantityonhand"),
                cancellationToken: cancellationToken);

            if (product == null)
            {
                _logger.LogDebug("Product not found for info lookup: {ProductId}", productId);
                return null;
            }

            var productInfo = new ProductInfo(
                Id: productId,
                Name: product.GetAttributeValue<string>("name") ?? "",
                IsActive: product.GetAttributeValue<OptionSetValue>("statecode")?.Value == 1, // Active = 1 for products
                Price: product.GetAttributeValue<Money>("price")?.Value ?? 0m,
                StockQuantity: (int)Math.Max(0, product.GetAttributeValue<decimal>("quantityonhand"))
            );

            _logger.LogDebug("Product info retrieved: {ProductId} - {Name} (Active: {IsActive}, Stock: {Stock})", 
                productId, productInfo.Name, productInfo.IsActive, productInfo.StockQuantity);
            return productInfo;
        }
        catch (Exception ex) when (IsEntityNotFoundError(ex))
        {
            _logger.LogDebug("Product not found for info lookup: {ProductId}", productId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product info: {ProductId}", productId);
            throw new InvalidOperationException($"Failed to get product info: {productId}", ex);
        }
    }

    /// <summary>
    /// Determines if an exception indicates an entity was not found
    /// </summary>
    private static bool IsEntityNotFoundError(Exception ex)
    {
        return ex.Message.Contains("does not exist") ||
               ex.Message.Contains("The record") ||
               (ex is FaultException<OrganizationServiceFault> fault && 
                fault.Detail.ErrorCode == -2147220969); // Entity not found error code
    }
}