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
            var command = EntityMapper.MapToCreateOrderCommand(targetEntity, tracingService);

            // ...existing code...
        }
        catch (Exception ex)
        {
            tracingService.Trace($"Error in CreateOrderPlugin: {ex.Message}");
            throw;
        }
    }
}
