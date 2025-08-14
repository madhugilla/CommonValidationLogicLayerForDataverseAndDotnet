# Complete Shared Validation Layer Solution

This solution demonstrates how to implement a common validation library shared between Dataverse plugins and ASP.NET Core applications.

## Solution Structure

```
SharedValidationExample/
├── src/
│   ├── Shared.Domain/                    # Common validation library
│   │   ├── Shared.Domain.csproj
│   │   ├── Domain/
│   │   │   └── Orders/
│   │   │       ├── CreateOrderCommand.cs
│   │   │       ├── IOrderRulesData.cs
│   │   │       └── CreateOrderValidator.cs
│   │   └── Common/
│   │       └── ValidationExtensions.cs
│   │
│   ├── Plugins.Dataverse/                # Dataverse plugin assembly
│   │   ├── Plugins.Dataverse.csproj
│   │   ├── Adapters/
│   │   │   └── DataverseOrderRulesData.cs
│   │   ├── Orders/
│   │   │   └── CreateOrderPlugin.cs
│   │   └── Mapping/
│   │       └── EntityMapper.cs
│   │
│   └── Api.Orders/                       # ASP.NET Core API
│       ├── Api.Orders.csproj
│       ├── Program.cs
│       ├── Controllers/
│       │   └── OrdersController.cs
│       ├── Models/
│       │   └── CreateOrderRequest.cs
│       ├── Adapters/
│       │   └── DataverseOrderRulesDataForApp.cs
│       └── Services/
│           ├── IOrderService.cs
│           └── DataverseOrderService.cs
│
├── tests/
│   ├── Shared.Domain.Tests/
│   │   ├── Shared.Domain.Tests.csproj
│   │   └── Orders/
│   │       └── CreateOrderValidatorTests.cs
│   │
│   └── Integration.Tests/
│       ├── Integration.Tests.csproj
│       └── OrderValidationIntegrationTests.cs
│
├── SharedValidationExample.sln
└── README.md
```

## Key Components

1. **Shared.Domain**: Contains all business validation logic, domain models, and interfaces
2. **Plugins.Dataverse**: Dataverse plugin that uses shared validation for transactional enforcement  
3. **Api.Orders**: ASP.NET Core API that uses shared validation for fail-fast scenarios
4. **Tests**: Unit and integration tests for the shared validation logic

## Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Access to a Dataverse environment (for testing)
- Plugin Registration Tool (for plugin deployment)

## Setup Instructions

1. Clone or create the solution structure
2. Restore NuGet packages for all projects
3. Configure Dataverse connection strings in appsettings.json
4. Build the solution
5. Deploy the plugin to Dataverse using Plugin Registration Tool
6. Run the API project and test validation

## Testing the Solution

The solution includes unit tests that mock the IOrderRulesData interface and integration tests that validate the end-to-end flow.