using FluentValidation;

namespace Shared.Domain.Orders;

/// <summary>
/// FluentValidation validator for CreateOrderCommand.
/// This contains ALL business validation rules and is shared between
/// Dataverse plugins and ASP.NET Core API.
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    private readonly IOrderRulesData _rulesData;

    public CreateOrderValidator(IOrderRulesData rulesData)
    {
        _rulesData = rulesData ?? throw new ArgumentNullException(nameof(rulesData));

        ValidateCustomer();
        ValidateOrderDate();
        ValidateOrderNumber();
        ValidateOrderLines();
        ValidateTotalAmount();
    }

    private void ValidateCustomer()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithErrorCode(OrderValidationErrors.CustomerRequired)
            .WithMessage("Customer ID is required.");

        RuleFor(x => x.CustomerId)
            .MustAsync(async (customerId, ct) => 
            {
                if (string.IsNullOrEmpty(customerId)) return true; // Let NotEmpty handle this
                return await _rulesData.CustomerExistsAsync(customerId, ct);
            })
            .WithErrorCode(OrderValidationErrors.CustomerNotFound)
            .WithMessage("Customer does not exist.")
            .When(x => !string.IsNullOrEmpty(x.CustomerId));

        // Advanced customer validation
        RuleFor(x => x.CustomerId)
            .MustAsync(async (customerId, ct) =>
            {
                if (string.IsNullOrEmpty(customerId)) return true;
                var customer = await _rulesData.TryGetCustomerInfoAsync(customerId, ct);
                return customer?.IsActive == true;
            })
            .WithErrorCode("CUSTOMER_INACTIVE")
            .WithMessage("Customer is inactive and cannot place orders.")
            .When(x => !string.IsNullOrEmpty(x.CustomerId));
    }

    private void ValidateOrderDate()
    {
        RuleFor(x => x.OrderDate)
            .Must(date => date >= DateTime.Today.AddDays(-1))
            .WithErrorCode(OrderValidationErrors.OrderDateInvalid)
            .WithMessage("Order date cannot be more than 1 day in the past.");

        RuleFor(x => x.OrderDate)
            .Must(date => date <= DateTime.Today.AddDays(30))
            .WithErrorCode(OrderValidationErrors.OrderDateInvalid)
            .WithMessage("Order date cannot be more than 30 days in the future.");
    }

    private void ValidateOrderNumber()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .WithErrorCode(OrderValidationErrors.OrderNumberRequired)
            .WithMessage("Order number is required.");

        RuleFor(x => x.OrderNumber)
            .Length(3, 50)
            .WithErrorCode(OrderValidationErrors.OrderNumberRequired)
            .WithMessage("Order number must be between 3 and 50 characters.");

        RuleFor(x => x.OrderNumber)
            .MustAsync(async (orderNumber, ct) =>
            {
                if (string.IsNullOrEmpty(orderNumber)) return true; // Let other rules handle this
                return await _rulesData.IsOrderNumberUniqueAsync(orderNumber, ct);
            })
            .WithErrorCode(OrderValidationErrors.OrderNumberNotUnique)
            .WithMessage("Order number must be unique.")
            .When(x => !string.IsNullOrEmpty(x.OrderNumber));
    }

    private void ValidateOrderLines()
    {
        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty()
            .WithErrorCode(OrderValidationErrors.LinesRequired)
            .WithMessage("At least one order line is required.");

        RuleFor(x => x.Lines)
            .Must(lines => lines?.Count <= 100)
            .WithErrorCode("TOO_MANY_LINES")
            .WithMessage("Order cannot have more than 100 lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            ValidateLineProduct(line);
            ValidateLineQuantity(line);
            ValidateLinePrice(line);
        });
    }

    private void ValidateLineProduct(InlineValidator<OrderLineCommand> line)
    {
        line.RuleFor(l => l.ProductId)
            .NotEmpty()
            .WithErrorCode(OrderValidationErrors.ProductRequired)
            .WithMessage("Product ID is required for each line.");

        line.RuleFor(l => l.ProductId)
            .MustAsync(async (productId, ct) =>
            {
                if (string.IsNullOrEmpty(productId)) return true; // Let NotEmpty handle this
                return await _rulesData.ProductExistsAsync(productId, ct);
            })
            .WithErrorCode(OrderValidationErrors.ProductNotFound)
            .WithMessage("Product does not exist.")
            .When(l => !string.IsNullOrEmpty(l.ProductId));

        // Check if product is active
        line.RuleFor(l => l.ProductId)
            .MustAsync(async (productId, ct) =>
            {
                if (string.IsNullOrEmpty(productId)) return true;
                var product = await _rulesData.TryGetProductInfoAsync(productId, ct);
                return product?.IsActive == true;
            })
            .WithErrorCode("PRODUCT_INACTIVE")
            .WithMessage("Product is inactive and cannot be ordered.")
            .When(l => !string.IsNullOrEmpty(l.ProductId));
    }

    private void ValidateLineQuantity(InlineValidator<OrderLineCommand> line)
    {
        line.RuleFor(l => l.Quantity)
            .GreaterThan(0)
            .WithErrorCode(OrderValidationErrors.QuantityInvalid)
            .WithMessage("Quantity must be greater than zero.");

        line.RuleFor(l => l.Quantity)
            .LessThanOrEqualTo(1000)
            .WithErrorCode(OrderValidationErrors.QuantityInvalid)
            .WithMessage("Quantity cannot exceed 1000.");

        // Stock validation
        line.RuleFor(l => l)
            .MustAsync(async (orderLine, ct) =>
            {
                if (string.IsNullOrEmpty(orderLine.ProductId) || orderLine.Quantity <= 0)
                    return true; // Let other rules handle these cases

                var product = await _rulesData.TryGetProductInfoAsync(orderLine.ProductId, ct);
                return product == null || product.StockQuantity >= orderLine.Quantity;
            })
            .WithErrorCode("INSUFFICIENT_STOCK")
            .WithMessage("Insufficient stock for requested quantity.");
    }

    private void ValidateLinePrice(InlineValidator<OrderLineCommand> line)
    {
        line.RuleFor(l => l.UnitPrice)
            .GreaterThan(0)
            .WithErrorCode(OrderValidationErrors.UnitPriceInvalid)
            .WithMessage("Unit price must be greater than zero.");

        // Price validation against catalog
        line.RuleFor(l => l)
            .MustAsync(async (orderLine, ct) =>
            {
                if (string.IsNullOrEmpty(orderLine.ProductId) || orderLine.UnitPrice <= 0)
                    return true; // Let other rules handle these cases

                var catalogPrice = await _rulesData.TryGetProductPriceAsync(orderLine.ProductId, ct);
                if (catalogPrice == null) return true; // Product doesn't exist, other rules handle this

                // Allow up to 10% discount from catalog price
                var minAllowedPrice = catalogPrice.Value * 0.9m;
                return orderLine.UnitPrice >= minAllowedPrice;
            })
            .WithErrorCode(OrderValidationErrors.UnitPriceTooLow)
            .WithMessage("Unit price is too low compared to catalog price.")
            .When(l => !string.IsNullOrEmpty(l.ProductId) && l.UnitPrice > 0);

        // Maximum price validation (prevent data entry errors)
        line.RuleFor(l => l)
            .MustAsync(async (orderLine, ct) =>
            {
                if (string.IsNullOrEmpty(orderLine.ProductId) || orderLine.UnitPrice <= 0)
                    return true;

                var catalogPrice = await _rulesData.TryGetProductPriceAsync(orderLine.ProductId, ct);
                if (catalogPrice == null) return true;

                // Don't allow more than 200% of catalog price (likely data entry error)
                var maxAllowedPrice = catalogPrice.Value * 2.0m;
                return orderLine.UnitPrice <= maxAllowedPrice;
            })
            .WithErrorCode("UNIT_PRICE_TOO_HIGH")
            .WithMessage("Unit price seems too high compared to catalog price.")
            .When(l => !string.IsNullOrEmpty(l.ProductId) && l.UnitPrice > 0);
    }

    private void ValidateTotalAmount()
    {
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0)
            .WithErrorCode(OrderValidationErrors.TotalAmountMismatch)
            .WithMessage("Total amount must be greater than zero.");

        RuleFor(x => x)
            .Must(order =>
            {
                if (order.Lines?.Any() != true) return true; // Let other rules handle this

                var calculatedTotal = order.Lines.Sum(l => l.LineTotal);
                return Math.Abs(order.TotalAmount - calculatedTotal) < 0.01m; // Allow for rounding differences
            })
            .WithErrorCode(OrderValidationErrors.TotalAmountMismatch)
            .WithMessage("Total amount does not match the sum of line totals.");
    }
}
