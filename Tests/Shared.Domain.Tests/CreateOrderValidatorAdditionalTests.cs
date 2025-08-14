using FluentAssertions;
using Moq;
using Shared.Domain.Orders;
using Xunit;

namespace Shared.Domain.Tests.Orders;

/// <summary>
/// Additional boundary & edge case tests for CreateOrderValidator.
/// Focus: boundaries, rounding tolerance, pass cases not previously asserted.
/// </summary>
public sealed class CreateOrderValidatorAdditionalTests
{
    private readonly Mock<IOrderRulesData> _rulesData = new();
    private CreateOrderValidator CreateValidator() => new(_rulesData.Object);

    private CreateOrderCommand BaseValid(decimal unitPrice = 10m, int qty = 2) => new(
        CustomerId: "customer-123",
        OrderDate: DateTime.Today,
        OrderNumber: "ORD-EDGE",
        TotalAmount: qty * unitPrice,
        Lines: new[] { new OrderLineCommand("product-1", qty, unitPrice) }.AsReadOnly()
    );

    private void SetupHappyPath(decimal catalogPrice = 10m, int stock = 100)
    {
        _rulesData.Setup(x => x.CustomerExistsAsync("customer-123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _rulesData.Setup(x => x.TryGetCustomerInfoAsync("customer-123", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new CustomerInfo("customer-123", "Test Customer", true, 1000m));
        _rulesData.Setup(x => x.IsOrderNumberUniqueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _rulesData.Setup(x => x.ProductExistsAsync("product-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _rulesData.Setup(x => x.TryGetProductInfoAsync("product-1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ProductInfo("product-1", "Product", true, catalogPrice, stock));
        _rulesData.Setup(x => x.TryGetProductPriceAsync("product-1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(catalogPrice);
    }

    [Fact]
    public async Task OrderDate_ExactlyOneDayPast_Passes()
    {
        SetupHappyPath();
        var cmd = BaseValid() with { OrderDate = DateTime.Today.AddDays(-1) };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task OrderDate_Exactly30DaysFuture_Passes()
    {
        SetupHappyPath();
        var cmd = BaseValid() with { OrderDate = DateTime.Today.AddDays(30) };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task OrderNumber_Length3And50_Pass()
    {
        SetupHappyPath();
        var v = CreateValidator();

        var shortCmd = BaseValid() with { OrderNumber = new string('A',3) };
        (await v.ValidateAsync(shortCmd)).IsValid.Should().BeTrue();

        var longCmd = BaseValid() with { OrderNumber = new string('B',50) };
        (await v.ValidateAsync(longCmd)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Lines_Count100_Pass()
    {
        SetupHappyPath();
        var lines = Enumerable.Range(1,100).Select(i => new OrderLineCommand("product-1",1,10m)).ToList();
        var cmd = BaseValid() with { Lines = lines.AsReadOnly(), TotalAmount = lines.Sum(l=>l.LineTotal) };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Lines_Null_Fails()
    {
        // Create order with null lines by bypassing helper
        var cmd = new CreateOrderCommand(
            CustomerId: "customer-123",
            OrderDate: DateTime.Today,
            OrderNumber: "ORD-NULL",
            TotalAmount: 0m,
            Lines: null!);

        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.Lines) && e.ErrorCode == OrderValidationErrors.LinesRequired);
    }

    [Fact]
    public async Task Product_Inactive_Fails()
    {
        SetupHappyPath();
        _rulesData.Setup(x => x.TryGetProductInfoAsync("product-1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ProductInfo("product-1","Prod", false, 10m, 100));

        var result = await CreateValidator().ValidateAsync(BaseValid());
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "PRODUCT_INACTIVE");
    }

    [Fact]
    public async Task Quantity_Exactly1000_Pass()
    {
        SetupHappyPath(stock: 2000);
        var cmd = BaseValid(qty:1000) with { TotalAmount = 1000 * 10m };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Stock_EqualsQuantity_Pass()
    {
        SetupHappyPath(stock: 5);
        var cmd = BaseValid(unitPrice:10m, qty:5) with { TotalAmount = 50m };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Discount_At10PercentBoundary_Pass()
    {
        var catalog = 100m; var discounted = catalog * 0.9m;
        SetupHappyPath(catalogPrice: catalog);
        var cmd = BaseValid(unitPrice: discounted, qty:1) with { TotalAmount = discounted };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Discount_JustBelow10Percent_Fails()
    {
        var catalog = 100m; var discounted = catalog * 0.899m; // slightly below allowed
        SetupHappyPath(catalogPrice: catalog);
        var cmd = BaseValid(unitPrice: discounted, qty:1) with { TotalAmount = discounted };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == OrderValidationErrors.UnitPriceTooLow);
    }

    [Fact]
    public async Task Price_At200PercentBoundary_Pass()
    {
        var catalog = 50m; var high = catalog * 2.0m;
        SetupHappyPath(catalogPrice: catalog);
        var cmd = BaseValid(unitPrice: high, qty:1) with { TotalAmount = high };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Price_JustAbove200Percent_Fails()
    {
        var catalog = 50m; var tooHigh = catalog * 2.01m;
        SetupHappyPath(catalogPrice: catalog);
        var cmd = BaseValid(unitPrice: tooHigh, qty:1) with { TotalAmount = tooHigh };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "UNIT_PRICE_TOO_HIGH");
    }

    [Fact]
    public async Task Price_NoCatalogPrice_SkipsRangeChecks()
    {
        SetupHappyPath();
        _rulesData.Setup(x => x.TryGetProductPriceAsync("product-1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((decimal?)null);
        var cmd = BaseValid(unitPrice: 999m, qty:1) with { TotalAmount = 999m };
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Total_RoundingTolerance_Pass()
    {
        SetupHappyPath();
        var cmd = new CreateOrderCommand(
            CustomerId: "customer-123",
            OrderDate: DateTime.Today,
            OrderNumber: "ORD-ROUND",
            TotalAmount: 10.00m, // Provided total
            Lines: new[] { new OrderLineCommand("product-1", 1, 10.005m) }.AsReadOnly());
        // Sum line = 10.005, diff 0.005 < 0.01 allowed
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Total_RoundingTolerance_Fail()
    {
        SetupHappyPath();
        var cmd = new CreateOrderCommand(
            CustomerId: "customer-123",
            OrderDate: DateTime.Today,
            OrderNumber: "ORD-ROUND-FAIL",
            TotalAmount: 10.00m,
            Lines: new[] { new OrderLineCommand("product-1", 1, 10.02m) }.AsReadOnly());
        // Diff 0.02 >= 0.01 -> fail
        var result = await CreateValidator().ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == OrderValidationErrors.TotalAmountMismatch);
    }
}
