using FluentAssertions;
using Moq;
using Shared.Domain.Orders;
using Xunit;

namespace Shared.Domain.Tests.Orders;

/// <summary>
/// Unit tests for CreateOrderValidator demonstrating how to test 
/// the shared validation logic in isolation using mocks
/// </summary>
public sealed class CreateOrderValidatorTests
{
    private readonly Mock<IOrderRulesData> _mockRulesData;
    private readonly CreateOrderValidator _validator;

    public CreateOrderValidatorTests()
    {
        _mockRulesData = new Mock<IOrderRulesData>();
        _validator = new CreateOrderValidator(_mockRulesData.Object);
    }

    [Fact]
    public async Task Validate_ValidOrder_ShouldPass()
    {
        // Arrange
        var command = CreateValidOrderCommand();
        SetupMockForValidOrder();

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_EmptyCustomerId_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand() with { CustomerId = "" };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.CustomerId) &&
            e.ErrorCode == OrderValidationErrors.CustomerRequired);
    }

    [Fact]
    public async Task Validate_NonExistentCustomer_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand();
        _mockRulesData.Setup(x => x.CustomerExistsAsync(command.CustomerId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(false);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.CustomerId) &&
            e.ErrorCode == OrderValidationErrors.CustomerNotFound);
    }

    [Fact]
    public async Task Validate_InactiveCustomer_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand();
        _mockRulesData.Setup(x => x.CustomerExistsAsync(command.CustomerId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _mockRulesData.Setup(x => x.TryGetCustomerInfoAsync(command.CustomerId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new CustomerInfo("customer-id", "Test Customer", IsActive: false, CreditLimit: 1000m));

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.CustomerId) &&
            e.ErrorCode == "CUSTOMER_INACTIVE");
    }

    [Theory]
    [InlineData(-2)] // More than 1 day in past
    [InlineData(31)] // More than 30 days in future
    public async Task Validate_InvalidOrderDate_ShouldFail(int daysFromToday)
    {
        // Arrange
        var command = CreateValidOrderCommand() with { OrderDate = DateTime.Today.AddDays(daysFromToday) };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.OrderDate) &&
            e.ErrorCode == OrderValidationErrors.OrderDateInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB")] // Too short
    [InlineData("This order number is way too long and exceeds the fifty character limit")] // Too long
    public async Task Validate_InvalidOrderNumber_ShouldFail(string orderNumber)
    {
        // Arrange
        var command = CreateValidOrderCommand() with { OrderNumber = orderNumber };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.OrderNumber));
    }

    [Fact]
    public async Task Validate_DuplicateOrderNumber_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand();
    // Setup valid scenario first then override uniqueness to false
    SetupMockForValidOrder(command.OrderNumber);
    _mockRulesData.Setup(x => x.IsOrderNumberUniqueAsync(command.OrderNumber, It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.OrderNumber) &&
            e.ErrorCode == OrderValidationErrors.OrderNumberNotUnique);
    }

    [Fact]
    public async Task Validate_EmptyLines_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand() with { Lines = Array.Empty<OrderLineCommand>() };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.Lines) &&
            e.ErrorCode == OrderValidationErrors.LinesRequired);
    }

    [Fact]
    public async Task Validate_TooManyLines_ShouldFail()
    {
        // Arrange
        var lines = Enumerable.Range(1, 101)
            .Select(i => new OrderLineCommand($"product-{i}", 1, 10m))
            .ToList();
        var command = CreateValidOrderCommand() with { Lines = lines.AsReadOnly() };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateOrderCommand.Lines) &&
            e.ErrorCode == "TOO_MANY_LINES");
    }

    [Fact]
    public async Task Validate_NonExistentProduct_ShouldFail()
    {
        // Arrange
        var command = CreateValidOrderCommand();
        SetupMockForValidOrder();
        _mockRulesData.Setup(x => x.ProductExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(false);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName.StartsWith("Lines[0].ProductId") &&
            e.ErrorCode == OrderValidationErrors.ProductNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Validate_InvalidQuantity_ShouldFail(int quantity)
    {
        // Arrange
        var line = new OrderLineCommand("product-1", quantity, 10m);
        var command = CreateValidOrderCommand() with { Lines = new[] { line }.AsReadOnly() };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName.StartsWith("Lines[0].Quantity") &&
            e.ErrorCode == OrderValidationErrors.QuantityInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_InvalidUnitPrice_ShouldFail(decimal unitPrice)
    {
        // Arrange
        var line = new OrderLineCommand("product-1", 1, unitPrice);
        var command = CreateValidOrderCommand() with { Lines = new[] { line }.AsReadOnly() };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName.StartsWith("Lines[0].UnitPrice") &&
            e.ErrorCode == OrderValidationErrors.UnitPriceInvalid);
    }

    [Fact]
    public async Task Validate_UnitPriceTooLow_ShouldFail()
    {
        // Arrange
        var catalogPrice = 100m;
        var tooLowPrice = catalogPrice * 0.4m; // 40% of catalog price (minimum allowed is 90%)
        var line = new OrderLineCommand("product-1", 1, tooLowPrice);
        var command = CreateValidOrderCommand() with { Lines = new[] { line }.AsReadOnly() };
        
        SetupMockForValidOrder();
        _mockRulesData.Setup(x => x.TryGetProductPriceAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(catalogPrice);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName.StartsWith("Lines[0]") &&
            e.ErrorCode == OrderValidationErrors.UnitPriceTooLow);
    }

    [Fact]
    public async Task Validate_UnitPriceTooHigh_ShouldFail()
    {
        // Arrange
        var catalogPrice = 100m;
        var tooHighPrice = catalogPrice * 2.5m; // 250% of catalog price (maximum allowed is 200%)
        var line = new OrderLineCommand("product-1", 1, tooHighPrice);
        var command = CreateValidOrderCommand() with { Lines = new[] { line }.AsReadOnly() };
        
        SetupMockForValidOrder();
        _mockRulesData.Setup(x => x.TryGetProductPriceAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(catalogPrice);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName.StartsWith("Lines[0]") &&
            e.ErrorCode == "UNIT_PRICE_TOO_HIGH");
    }

    [Fact]
    public async Task Validate_InsufficientStock_ShouldFail()
    {
        // Arrange
        var line = new OrderLineCommand("product-1", 100, 10m); // Order 100 units
        var command = CreateValidOrderCommand() with { Lines = new[] { line }.AsReadOnly() };
        
        SetupMockForValidOrder();
        _mockRulesData.Setup(x => x.TryGetProductInfoAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProductInfo("product-1", "Test Product", IsActive: true, Price: 10m, StockQuantity: 50)); // Only 50 in stock

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName.StartsWith("Lines[0]") &&
            e.ErrorCode == "INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Validate_TotalAmountMismatch_ShouldFail()
    {
        // Arrange
        var line = new OrderLineCommand("product-1", 2, 10m); // Should total 20
        var command = CreateValidOrderCommand() with 
        { 
            Lines = new[] { line }.AsReadOnly(),
            TotalAmount = 25m // Incorrect total
        };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            (e.PropertyName == nameof(CreateOrderCommand) || e.PropertyName == string.Empty) &&
            e.ErrorCode == OrderValidationErrors.TotalAmountMismatch);
    }

    [Fact]
    public async Task Validate_MultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: "", // Invalid
            OrderDate: DateTime.Today.AddDays(-5), // Invalid
            OrderNumber: "A", // Too short
            TotalAmount: 0, // Invalid
            Lines: Array.Empty<OrderLineCommand>() // Empty
        );

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
        
        // Should have errors for customer, order date, order number, total amount, and lines
        result.Errors.Select(e => e.PropertyName).Should().Contain(new[]
        {
            nameof(CreateOrderCommand.CustomerId),
            nameof(CreateOrderCommand.OrderDate),
            nameof(CreateOrderCommand.OrderNumber),
            nameof(CreateOrderCommand.TotalAmount),
            nameof(CreateOrderCommand.Lines)
        });
    }

    private CreateOrderCommand CreateValidOrderCommand()
    {
        return new CreateOrderCommand(
            CustomerId: "customer-123",
            OrderDate: DateTime.Today,
            OrderNumber: "ORD-001",
            TotalAmount: 20m,
            Lines: new[]
            {
                new OrderLineCommand("product-1", 2, 10m)
            }.AsReadOnly()
        );
    }

    private void SetupMockForValidOrder(string? overrideOrderNumber = null)
    {
        var orderNumber = overrideOrderNumber ?? "ORD-001";
        
        // Customer exists and is active
        _mockRulesData.Setup(x => x.CustomerExistsAsync("customer-123", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _mockRulesData.Setup(x => x.TryGetCustomerInfoAsync("customer-123", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new CustomerInfo("customer-123", "Test Customer", IsActive: true, CreditLimit: 1000m));

        // Order number is unique
        _mockRulesData.Setup(x => x.IsOrderNumberUniqueAsync(orderNumber, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);

        // Product exists and is active with sufficient stock
        _mockRulesData.Setup(x => x.ProductExistsAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _mockRulesData.Setup(x => x.TryGetProductInfoAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ProductInfo("product-1", "Test Product", IsActive: true, Price: 10m, StockQuantity: 100));
        _mockRulesData.Setup(x => x.TryGetProductPriceAsync("product-1", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(10m);
    }
}