using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Plugins.Dataverse.Adapters;
using Plugins.Dataverse.Mapping;
using Shared.Domain.Orders;
using Shared.Domain.Common;

namespace Plugins.Dataverse.Orders;

/// <summary>
/// Dataverse plugin that validates order creation using the shared validation library.
/// Register this plugin on Create of the Order entity in PreValidation or PreOperation stage.
/// </summary>
public sealed class CreateOrderPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // Get plugin execution context and services
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext))!;
        var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory))!;
        var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService))!;
        var organizationService = serviceFactory.CreateOrganizationService(context.UserId);

        tracingService.Trace("CreateOrderPlugin execution started");

        try
        {
            // Only process Create operations
            if (context.MessageName != "Create")
            {
                tracingService.Trace($"Skipping non-Create message: {context.MessageName}");
                return;
            }

            // Get the Target entity
            if (!context.InputParameters.Contains("Target") || 
                context.InputParameters["Target"] is not Entity targetEntity)
            {
                tracingService.Trace("No Target entity found in InputParameters");
                return;
            }

            tracingService.Trace($"Processing {context.MessageName} for entity: {targetEntity.LogicalName}");

            // Validate we're working with the correct entity
            if (targetEntity.LogicalName != "new_order") // Adjust to your actual order entity name
            {
                tracingService.Trace($"Skipping validation for entity: {targetEntity.LogicalName}");
                return;
            }

            // Check if entity has required fields for validation
            if (!EntityMapper.HasRequiredOrderFields(targetEntity))
            {
                tracingService.Trace("Entity missing required fields for validation");
                return;
            }

            // Map entity to domain command
            tracingService.Trace("Mapping entity to CreateOrderCommand");
            var createOrderCommand = EntityMapper.MapToCreateOrderCommand(targetEntity, tracingService);

            // Create validation infrastructure
            var rulesData = new DataverseOrderRulesData(organizationService, tracingService);
            var validator = new CreateOrderValidator(rulesData);

            // Execute validation
            tracingService.Trace("Starting order validation");
            var validationResult = validator.Validate(createOrderCommand);

            // Handle validation results
            if (validationResult.IsValid)
            {
                tracingService.Trace("Order validation passed");
                
                // Optional: Normalize/clean data before save
                NormalizeOrderData(targetEntity, tracingService);
            }
            else
            {
                // Validation failed - block the operation
                var errorMessage = validationResult.GetErrorsAsString();
                var errorCodes = validationResult.GetErrorCodes();
                
                tracingService.Trace($"Order validation failed. Errors: {errorMessage}");
                tracingService.Trace($"Error codes: {string.Join(", ", errorCodes)}");

                // Throw exception to block the operation and rollback transaction
                throw new InvalidPluginExecutionException($"Order validation failed: {errorMessage}");
            }
        }
        catch (InvalidPluginExecutionException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            tracingService.Trace($"Unexpected error in CreateOrderPlugin: {ex.Message}");
            tracingService.Trace($"Stack trace: {ex.StackTrace}");
            
            // Don't block operations due to validation system errors in production
            // In development, you might want to throw to catch issues
            throw new InvalidPluginExecutionException($"Order validation system error: {ex.Message}");
        }
        finally
        {
            tracingService.Trace("CreateOrderPlugin execution completed");
        }
    }

    /// <summary>
    /// Normalizes/cleans order data before save (optional)
    /// This runs only if validation passes
    /// </summary>
    private static void NormalizeOrderData(Entity orderEntity, ITracingService tracingService)
    {
        tracingService.Trace("Starting data normalization");

        try
        {
            // Round monetary values to 2 decimal places
            if (orderEntity.Contains("new_totalamount") && 
                orderEntity["new_totalamount"] is Money totalAmount)
            {
                var roundedAmount = Math.Round(totalAmount.Value, 2);
                if (Math.Abs(totalAmount.Value - roundedAmount) > 0.001m)
                {
                    orderEntity["new_totalamount"] = new Money(roundedAmount);
                    tracingService.Trace($"Rounded total amount from {totalAmount.Value} to {roundedAmount}");
                }
            }

            // Round unit price
            if (orderEntity.Contains("new_unitprice") && 
                orderEntity["new_unitprice"] is Money unitPrice)
            {
                var roundedPrice = Math.Round(unitPrice.Value, 2);
                if (Math.Abs(unitPrice.Value - roundedPrice) > 0.001m)
                {
                    orderEntity["new_unitprice"] = new Money(roundedPrice);
                    tracingService.Trace($"Rounded unit price from {unitPrice.Value} to {roundedPrice}");
                }
            }

            // Ensure order date is not in the future beyond reasonable limits
            if (orderEntity.Contains("new_orderdate") && 
                orderEntity["new_orderdate"] is DateTime orderDate)
            {
                var maxFutureDate = DateTime.Today.AddDays(1);
                if (orderDate > maxFutureDate)
                {
                    orderEntity["new_orderdate"] = maxFutureDate;
                    tracingService.Trace($"Adjusted order date from {orderDate} to {maxFutureDate}");
                }
            }

            // Trim and clean order number
            if (orderEntity.Contains("new_ordernumber") && 
                orderEntity["new_ordernumber"] is string orderNumber)
            {
                var cleanedOrderNumber = orderNumber.Trim().ToUpperInvariant();
                if (cleanedOrderNumber != orderNumber)
                {
                    orderEntity["new_ordernumber"] = cleanedOrderNumber;
                    tracingService.Trace($"Cleaned order number from '{orderNumber}' to '{cleanedOrderNumber}'");
                }
            }

            tracingService.Trace("Data normalization completed");
        }
        catch (Exception ex)
        {
            tracingService.Trace($"Error during data normalization: {ex.Message}");
            // Don't fail the operation due to normalization errors
        }
    }
}

/// <summary>
/// Plugin for order updates - validates changes to existing orders
/// Register this plugin on Update of the Order entity in PreOperation stage
/// </summary>
public sealed class UpdateOrderPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext))!;
        var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory))!;
        var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService))!;
        var organizationService = serviceFactory.CreateOrganizationService(context.UserId);

        tracingService.Trace("UpdateOrderPlugin execution started");

        try
        {
            if (context.MessageName != "Update")
            {
                tracingService.Trace($"Skipping non-Update message: {context.MessageName}");
                return;
            }

            if (!context.InputParameters.Contains("Target") || 
                context.InputParameters["Target"] is not Entity targetEntity)
            {
                tracingService.Trace("No Target entity found in InputParameters");
                return;
            }

            if (targetEntity.LogicalName != "new_order")
            {
                tracingService.Trace($"Skipping validation for entity: {targetEntity.LogicalName}");
                return;
            }

            // For updates, we need to merge Target with PreImage to get complete record
            Entity completeEntity;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                completeEntity = context.PreEntityImages["PreImage"];
                // Merge changes from Target
                foreach (var attribute in targetEntity.Attributes)
                {
                    completeEntity[attribute.Key] = attribute.Value;
                }
                tracingService.Trace("Merged Target with PreImage for validation");
            }
            else
            {
                // No PreImage available, use Target only (less reliable)
                completeEntity = targetEntity;
                tracingService.Trace("No PreImage available, using Target only");
            }

            // Map and validate
            var createOrderCommand = EntityMapper.MapToCreateOrderCommand(completeEntity, tracingService);
            var rulesData = new DataverseOrderRulesData(organizationService, tracingService);
            var validator = new CreateOrderValidator(rulesData);

            var validationResult = validator.Validate(createOrderCommand);

            if (!validationResult.IsValid)
            {
                var errorMessage = validationResult.GetErrorsAsString();
                tracingService.Trace($"Order update validation failed: {errorMessage}");
                throw new InvalidPluginExecutionException($"Order update validation failed: {errorMessage}");
            }

            NormalizeOrderData(targetEntity, tracingService);
            tracingService.Trace("Order update validation passed");
        }
        catch (InvalidPluginExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            tracingService.Trace($"Unexpected error in UpdateOrderPlugin: {ex.Message}");
            throw new InvalidPluginExecutionException($"Order update validation system error: {ex.Message}");
        }
        finally
        {
            tracingService.Trace("UpdateOrderPlugin execution completed");
        }
    }
}