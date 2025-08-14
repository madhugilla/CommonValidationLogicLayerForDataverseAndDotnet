using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Shared.Domain.Orders;

namespace Plugins.Dataverse.Adapters;

/// <summary>
/// Dataverse implementation of IOrderRulesData for use in plugins.
/// Uses IOrganizationService to query Dataverse entities.
/// </summary>
public sealed class DataverseOrderRulesData : IOrderRulesData
{
    private readonly IOrganizationService _service;
    private readonly ITracingService? _tracing;

    public DataverseOrderRulesData(IOrganizationService service, ITracingService? tracing = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _tracing = tracing;
    }

    public Task<bool> CustomerExistsAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId) || !Guid.TryParse(customerId, out var customerGuid))
                return Task.FromResult(false);

            _tracing?.Trace($"Checking if customer exists: {customerId}");
            
            // Use retrieve with minimal columns for existence check
            _service.Retrieve("account", customerGuid, new ColumnSet(false));
            
            _tracing?.Trace($"Customer exists: {customerId}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Customer not found: {customerId} - {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> ProductExistsAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                return Task.FromResult(false);

            _tracing?.Trace($"Checking if product exists: {productId}");
            
            _service.Retrieve("product", productGuid, new ColumnSet(false));
            
            _tracing?.Trace($"Product exists: {productId}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Product not found: {productId} - {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<decimal?> TryGetProductPriceAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                return Task.FromResult<decimal?>(null);

            _tracing?.Trace($"Getting product price: {productId}");
            
            var product = _service.Retrieve("product", productGuid, new ColumnSet("price"));
            var price = product.GetAttributeValue<Money>("price")?.Value;
            
            _tracing?.Trace($"Product price retrieved: {productId} = {price}");
            return Task.FromResult(price);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Failed to get product price: {productId} - {ex.Message}");
            return Task.FromResult<decimal?>(null);
        }
    }

    public Task<bool> IsOrderNumberUniqueAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(orderNumber))
                return Task.FromResult(false);

            _tracing?.Trace($"Checking order number uniqueness: {orderNumber}");

            var query = new QueryExpression("new_order")
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

            var results = _service.RetrieveMultiple(query);
            var isUnique = results.Entities.Count == 0;
            
            _tracing?.Trace($"Order number uniqueness check: {orderNumber} = {isUnique}");
            return Task.FromResult(isUnique);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Failed to check order number uniqueness: {orderNumber} - {ex.Message}");
            // Default to not unique on error to be safe
            return Task.FromResult(false);
        }
    }

    public Task<CustomerInfo?> TryGetCustomerInfoAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId) || !Guid.TryParse(customerId, out var customerGuid))
                return Task.FromResult<CustomerInfo?>(null);

            _tracing?.Trace($"Getting customer info: {customerId}");

            var customer = _service.Retrieve("account", customerGuid, 
                new ColumnSet("name", "statecode", "creditlimit"));

            var info = new CustomerInfo(
                Id: customerId,
                Name: customer.GetAttributeValue<string>("name") ?? "",
                IsActive: customer.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0, // Active = 0
                CreditLimit: customer.GetAttributeValue<Money>("creditlimit")?.Value ?? 0m
            );

            _tracing?.Trace($"Customer info retrieved: {customerId} - {info.Name} (Active: {info.IsActive})");
            return Task.FromResult<CustomerInfo?>(info);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Failed to get customer info: {customerId} - {ex.Message}");
            return Task.FromResult<CustomerInfo?>(null);
        }
    }

    public Task<ProductInfo?> TryGetProductInfoAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || !Guid.TryParse(productId, out var productGuid))
                return Task.FromResult<ProductInfo?>(null);

            _tracing?.Trace($"Getting product info: {productId}");

            var product = _service.Retrieve("product", productGuid, 
                new ColumnSet("name", "statecode", "price", "quantityonhand"));

            var quantityRaw = product.Contains("quantityonhand") ? product["quantityonhand"] : null;
            int stockQuantity = 0;
            if (quantityRaw is int qi) stockQuantity = qi;
            else if (quantityRaw is decimal qd) stockQuantity = (int)qd;

            var info = new ProductInfo(
                Id: productId,
                Name: product.GetAttributeValue<string>("name") ?? "",
                IsActive: product.GetAttributeValue<OptionSetValue>("statecode")?.Value == 1, // Active = 1 for products
                Price: product.GetAttributeValue<Money>("price")?.Value ?? 0m,
                StockQuantity: stockQuantity
            );

            _tracing?.Trace($"Product info retrieved: {productId} - {info.Name} (Active: {info.IsActive}, Stock: {info.StockQuantity})");
            return Task.FromResult<ProductInfo?>(info);
        }
        catch (Exception ex)
        {
            _tracing?.Trace($"Failed to get product info: {productId} - {ex.Message}");
            return Task.FromResult<ProductInfo?>(null);
        }
    }
}