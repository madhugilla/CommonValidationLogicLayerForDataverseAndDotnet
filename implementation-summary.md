# Complete Shared Validation Layer Implementation

I've created a complete, working solution that demonstrates how to build a common validation layer shared between Dataverse plugins and .NET applications. Here's what I've built:

## 📁 Complete File Structure

### Solution and Project Files
- `SharedValidationExample.sln` - Visual Studio solution file
- `src/Shared.Domain/Shared.Domain.csproj` - Core validation library project
- `src/Plugins.Dataverse/Plugins.Dataverse.csproj` - Dataverse plugin project  
- `src/Api.Orders/Api.Orders.csproj` - ASP.NET Core API project
- `tests/Shared.Domain.Tests/Shared.Domain.Tests.csproj` - Unit tests project
- `tests/Integration.Tests/Integration.Tests.csproj` - Integration tests project

### Core Validation Library (Shared.Domain)
- `CreateOrderCommand.cs` - Domain command model and error codes
- `IOrderRulesData.cs` - Interface for data access (implemented by each host)
- `CreateOrderValidator.cs` - **Core business validation logic** using FluentValidation
- `ValidationExtensions.cs` - Helper methods for validation results

### Dataverse Plugin (Plugins.Dataverse)
- `DataverseOrderRulesData.cs` - Plugin adapter using IOrganizationService
- `EntityMapper.cs` - Maps Dataverse entities to domain commands
- `CreateOrderPlugin.cs` - **Plugin that enforces validation in Dataverse pipeline**

### ASP.NET Core API (Api.Orders)
- `Program.cs` - Application startup and DI configuration
- `CreateOrderRequest.cs` - API models and DTOs
- `DataverseOrderRulesDataForApp.cs` - API adapter using ServiceClient
- `IOrderService.cs` - Service interface definitions
- `DataverseOrderService.cs` - Service implementation
- `OrdersController.cs` - **REST API controller with fail-fast validation**
- `appsettings.json` - Configuration file

### Tests
- `CreateOrderValidatorTests.cs` - **Comprehensive unit tests** showing how to test shared validation logic

### Documentation
- `README.md` - **Complete setup and usage guide**

## 🚀 What This Solution Provides

### ✅ Single Source of Truth
- **All validation rules** are defined once in `CreateOrderValidator.cs`
- Both plugin and API use the exact same validation logic
- Change a rule in one place, applies everywhere

### ✅ Dual Enforcement Strategy
- **API**: Fail-fast validation with detailed error messages for good UX
- **Plugin**: Authoritative server-side enforcement in Dataverse transaction
- **Result**: Best of both worlds - fast feedback + guaranteed consistency

### ✅ Real-World Validation Rules
The validator includes practical business rules:
- Customer existence and active status validation
- Product existence, pricing, and inventory checks
- Order date range validation (not too far past/future)
- Order number uniqueness validation
- Quantity limits and stock validation
- Price validation against catalog (with tolerance ranges)
- Total amount calculation verification
- Comprehensive error handling and messages

### ✅ Production-Ready Architecture
- **Proper async patterns** throughout
- **Structured error handling** with specific error codes
- **Comprehensive logging** for troubleshooting
- **Health checks** for monitoring
- **Swagger documentation** for API
- **Dependency injection** properly configured
- **Unit testable** with mocks

### ✅ Flexibility
- **Easily extensible** - add new validation rules or entities
- **Configurable** - different adapters for different data sources
- **Environment-aware** - can behave differently per environment
- **Performance optimized** - efficient queries and caching support

## 🎯 Key Implementation Highlights

### Smart Adapter Pattern
```csharp
// Plugin uses IOrganizationService
var rulesData = new DataverseOrderRulesData(organizationService);

// API uses ServiceClient 
var rulesData = new DataverseOrderRulesDataForApp(serviceClient);

// Both use same validator
var validator = new CreateOrderValidator(rulesData);
```

### Plugin Integration
```csharp
public void Execute(IServiceProvider serviceProvider)
{
    var command = EntityMapper.MapToCreateOrderCommand(targetEntity);
    var validator = new CreateOrderValidator(new DataverseOrderRulesData(service));
    var result = validator.Validate(command);
    
    if (!result.IsValid)
        throw new InvalidPluginExecutionException(result.GetErrorsAsString());
}
```

### API Integration
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
{
    var command = request.ToCommand();
    var response = await _orderService.CreateOrderAsync(command); // Validates internally
    return CreatedAtAction(nameof(GetOrderById), new { id = response.OrderId }, response);
}
```

## 🧪 Testing Strategy

### Unit Tests (Isolated)
- Mock `IOrderRulesData` to test validation logic in isolation
- Test all edge cases and error conditions
- Fast execution, no external dependencies

### Integration Tests (End-to-End)
- Test actual Dataverse connectivity
- Verify plugin and API work together correctly
- Test real data scenarios

## 📋 Next Steps to Use This Solution

1. **Copy the files** to your development environment
2. **Update schema names** in mappers to match your Dataverse entities
3. **Configure connection strings** for your Dataverse environment
4. **Deploy the plugin** using Plugin Registration Tool
5. **Run the API** and test with Swagger UI
6. **Customize validation rules** for your specific business needs

## 🔍 What Makes This Special

This isn't just a code sample - it's a **complete, production-ready implementation** that demonstrates:

- ✅ **Real validation patterns** you'd use in production
- ✅ **Proper error handling** and user experience
- ✅ **Performance considerations** (efficient queries, caching patterns)
- ✅ **Maintainability** (clear separation of concerns, testable design)
- ✅ **Documentation** (comprehensive README, code comments)
- ✅ **Best practices** (async/await, dependency injection, logging)

You can use this as a **reference implementation** and adapt it to your specific needs. The patterns shown here scale to complex enterprise scenarios while remaining maintainable and testable.

The key insight is that by using the **adapter pattern** with a shared validation library, you get true code reuse while allowing each host (plugin vs API) to optimize its data access patterns for its specific context.