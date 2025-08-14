# Shared Validation Layer for Dataverse and .NET Applications

This solution demonstrates how to implement a **single source of truth** for business validation rules that can be shared between Dataverse plugins and .NET applications. The same validation logic runs in both contexts, ensuring consistency while avoiding code duplication.

## 🎯 Key Benefits

- **Single Source of Truth**: All business rules live in one shared library
- **Consistent Validation**: Same rules apply whether data comes from Dataverse forms, Power Apps, Power Automate, or your API
- **Fail-Fast Architecture**: API validates early to provide immediate feedback, while plugins provide authoritative server-side enforcement
- **Testable**: Core validation logic can be unit tested in isolation using mocks
- **Maintainable**: Change a business rule once and it applies everywhere

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Shared.Domain Library                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ Domain Models   │  │ IOrderRulesData │  │ FluentValidation│ │
│  │ (Commands/DTOs) │  │ (Abstraction)   │  │ Validators      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
              │                           │
              ▼                           ▼
┌─────────────────────────────┐  ┌─────────────────────────────┐
│     Dataverse Plugin        │  │      ASP.NET Core API       │
│                             │  │                             │
│ ┌─────────────────────────┐ │  │ ┌─────────────────────────┐ │
│ │ DataverseOrderRulesData │ │  │ │DataverseOrderRulesData  │ │
│ │ (IOrganizationService)  │ │  │ │ ForApp (ServiceClient)  │ │
│ └─────────────────────────┘ │  │ └─────────────────────────┘ │
│                             │  │                             │
│ • PreValidation/PreOp Stage │  │ • Controller Validation     │
│ • Blocks invalid saves      │  │ • Early failure (fail-fast) │
│ • Transactional enforcement │  │ • Detailed error responses  │
└─────────────────────────────┘  └─────────────────────────────┘
              │                           │
              ▼                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Dataverse                                  │
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Access to a Dataverse environment
- Plugin Registration Tool (for deploying plugins)

### 1. Clone and Build

```bash
git clone <repository-url>
cd SharedValidationExample
dotnet restore
dotnet build
```

### 2. Configure Dataverse Connection

The repository uses an environment-variable placeholder in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Dataverse": "${DATAVERSE_CONNECTION_STRING}"
  }
}
```

Create a user secret or environment variable:

Windows (PowerShell):

```powershell
$env:DATAVERSE_CONNECTION_STRING = "AuthType=OAuth;..."
```

Linux/macOS:

```bash
export DATAVERSE_CONNECTION_STRING="AuthType=OAuth;..."
```

User Secrets (in `src/Api.Orders`):

```bash
dotnet user-secrets set "ConnectionStrings:Dataverse" "AuthType=OAuth;..." --project src/Api.Orders/Api.Orders.csproj
```

Prefer secure flows (Client Secret or Certificate) in production; avoid username/password.

### 3. Create Dataverse Schema

Create the following entities in your Dataverse environment:

**Order Entity (`new_order`)**:

- `new_customerid` (Customer lookup to Account)
- `new_orderdate` (Date)
- `new_ordernumber` (Text)
- `new_totalamount` (Currency)
- `new_productid` (Product lookup)
- `new_quantity` (Whole Number)
- `new_unitprice` (Currency)

### 4. Deploy the Plugin

1. Build the `Plugins.Dataverse` project
2. Use Plugin Registration Tool to register:
   - **Assembly**: `Plugins.Dataverse.dll`
   - **Plugin**: `Plugins.Dataverse.Orders.CreateOrderPlugin`
   - **Message**: Create
   - **Entity**: new_order
   - **Stage**: PreValidation (recommended) or PreOperation
   - **Mode**: Synchronous

### 5. Run the API

```bash
cd src/Api.Orders
dotnet run
```

The API will be available at `https://localhost:7000` with Swagger UI at the root.

## 🧪 Testing the Solution

### Unit Tests

Run the shared validation tests (currently 38 passing tests covering boundaries, error paths, and success cases):

```bash
cd tests/Shared.Domain.Tests
dotnet test
```

### API Testing

Test the API endpoints:

```bash
# Create an order (will validate using shared rules)
curl -X POST https://localhost:7000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "customer-guid-here",
    "orderNumber": "ORD-001",
    "totalAmount": 20.00,
    "lines": [{
      "productId": "product-guid-here",
      "quantity": 2,
      "unitPrice": 10.00
    }]
  }'

# Validate without creating
curl -X POST https://localhost:7000/api/orders/validate \
  -H "Content-Type: application/json" \
  -d '{...same payload...}'
```

### Plugin Testing

Create an order through:

- Dataverse forms
- Power Apps
- Power Automate
- Direct SDK calls

The same validation rules will apply and block invalid data.

## 📋 How It Works

### 1. Shared Validation Logic

The `Shared.Domain` library contains:

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator(IOrderRulesData rulesData)
    {
        // All business rules defined here
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await rulesData.CustomerExistsAsync(id, ct))
            .WithMessage("Customer does not exist.");
        
        // ... more rules
    }
}
```

### 2. Dataverse Plugin Enforcement

```csharp
public void Execute(IServiceProvider serviceProvider)
{
    // Get Dataverse services
    var service = GetOrganizationService(serviceProvider);
    var target = GetTargetEntity(context);
    
    // Map to domain command
    var command = EntityMapper.MapToCreateOrderCommand(target);
    
    // Use shared validation
    var validator = new CreateOrderValidator(new DataverseOrderRulesData(service));
    var result = validator.Validate(command);
    
    if (!result.IsValid)
    {
        // Block the save
        throw new InvalidPluginExecutionException(result.GetErrorsAsString());
    }
}
```

### 3. API Fail-Fast Validation

```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
{
    var command = request.ToCommand();
    
    // Same validator, different data adapter
    var result = await _validator.ValidateAsync(command);
    
    if (!result.IsValid)
    {
        // Return 400 with detailed errors
        return ValidationProblem(result.ToErrorDictionary());
    }
    
    // Create the order (plugin will validate again as final guard)
    var response = await _orderService.CreateOrderAsync(command);
    return CreatedAtAction(nameof(GetOrderById), new { id = response.OrderId }, response);
}
```

## 🔧 Customizing for Your Needs

### Adding New Validation Rules

1. **Update the validator** in `Shared.Domain/Orders/CreateOrderValidator.cs`
2. **Add any new data requirements** to `IOrderRulesData`
3. **Implement in both adapters** (plugin and API)
4. **Add unit tests** in `Shared.Domain.Tests`

### Supporting Different Entities

1. **Create new command models** (e.g., `CreateCustomerCommand`)
2. **Create new validators** (e.g., `CreateCustomerValidator`)
3. **Create new adapters** for data access
4. **Create new plugins and controllers**

### Advanced Scenarios

- **Async validation**: Already supported via `MustAsync` in FluentValidation
- **Cross-entity validation**: Implement in `IOrderRulesData` adapters
- **Conditional validation**: Use FluentValidation's `When` conditions
- **Custom validation**: Implement `CustomAsync` rules for complex logic

## 🎯 Best Practices

### Plugin Development

- **Use PreValidation** stage when possible (cheaper than PreOperation rollbacks)
- **Keep plugins fast** - avoid heavy computations or external calls
- **Use proper error handling** - return user-friendly messages
- **Include tracing** for debugging
- **Filter attributes** in plugin steps to avoid unnecessary executions

### API Development

- **Validate early** in controllers before expensive operations
- **Return structured errors** using ValidationProblemDetails
- **Use async patterns** throughout the validation chain
- **Implement proper logging** for troubleshooting
- **Consider caching** for frequently accessed reference data

### Testing

- **Mock `IOrderRulesData`** for isolated unit tests
- **Test edge cases** thoroughly (null values, boundary conditions)
- **Integration test both paths** (plugin and API) against actual Dataverse
- **Performance test** with realistic data volumes

## 🧬 Multi-Targeting Rationale

`Shared.Domain` targets both `net8.0` and `netstandard2.0` so it can be consumed by:

- Modern .NET 8 API (leveraging latest runtime features)
- Legacy `net472` plugin via `netstandard2.0` surface (compatible with classic Dataverse plugin host)

Records are preserved by adding an `IsExternalInit` polyfill for the `netstandard2.0` target, avoiding refactors to classes while keeping modern C# expressiveness.

## 📚 Key Files Reference

| File | Purpose |
|------|---------|
| `Shared.Domain/Orders/CreateOrderValidator.cs` | **Core validation logic** - single source of truth for all business rules |
| `Shared.Domain/Orders/IOrderRulesData.cs` | **Data access abstraction** - defines what data validators need |
| `Plugins.Dataverse/Orders/CreateOrderPlugin.cs` | **Plugin implementation** - enforces validation in Dataverse pipeline |
| `Api.Orders/Controllers/OrdersController.cs` | **API controller** - validates before sending to Dataverse |
| `Plugins.Dataverse/Adapters/DataverseOrderRulesData.cs` | **Plugin data adapter** - implements IOrderRulesData using IOrganizationService |
| `Api.Orders/Adapters/DataverseOrderRulesDataForApp.cs` | **API data adapter** - implements IOrderRulesData using ServiceClient |
| `Tests/Shared.Domain.Tests/CreateOrderValidatorAdditionalTests.cs` | **Extended test coverage** for boundaries & edge cases |

## 🔒 Security & Secrets

Do not commit real credentials. Connection string is externalized. Recommended improvements:

- Use Azure Key Vault or environment variables in hosting environment
- Store secrets only in CI secret store (GitHub Actions Secrets)
- Enforce HTTPS and strict TLS settings
- Add static code analysis (CodeQL)

## 🤖 CI/CD (Suggested)

Workflow defined in `.github/workflows/ci.yml` executes on pushes and PRs to `main` or `master`:

- Restore -> Build (Release) -> Test (with coverage collection)
- Publishes test results & coverage (Cobertura + lcov) as artifacts
- Separate job lints `README.md` using markdown-lint

You can extend by:

- Adding Codecov upload (needs CODECOV_TOKEN secret)
- Enabling dependabot for NuGet & GitHub Actions
- Adding security scanning (CodeQL workflow)

## 🗺️ Roadmap (Ideas)

- Implement real order line entity persistence & retrieval
- Add credit limit validation using customer financials
- Introduce caching layer for product/customer lookups
- Provide integration test project hitting a Dataverse sandbox (flagged to skip in CI without env vars)
- Add OpenAPI schema filtering for validation error codes
- Provide sample Power Automate flow invoking API

## ⚠️ Troubleshooting Additions

If environment variable not picked up:

- Ensure terminal session set it before running `dotnet run`
- On Windows, consider using System Environment Variables if launching via IDE

If plugin cannot load assembly:

- Confirm target framework remains `net472`
- Ensure no accidental reference to `net8.0`-only APIs in plugin project

If validation differs between API and plugin:

- Check both adapters (`DataverseOrderRulesData` vs `DataverseOrderRulesDataForApp`) return matching data for same IDs


## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Troubleshooting

### Common Issues

**Plugin not firing**: Check plugin registration, message, entity, and stage configuration.

**Connection string errors**: Verify Dataverse URL, credentials, and AppId in connection string.

**Validation not working**: Ensure both adapters implement IOrderRulesData correctly and return consistent results.

**Performance issues**:

- Use column sets to limit data retrieval
- Implement caching for reference data
- Avoid N+1 query patterns

### Getting Help

- Check the unit tests for usage examples
- Review the Swagger documentation at the API root
- Enable detailed logging to troubleshoot validation failures
- Use Dataverse tracing to debug plugin execution
