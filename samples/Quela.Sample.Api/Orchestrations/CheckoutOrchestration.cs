using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Effects;
using Quela.Reactive.Core.Validation;
using Quela.Reactive.Core.Graph.Nodes;
using Quela.Reactive.DSL;
using Quela.Reactive.DSL.Extensions;

namespace Quela.Sample.Api.Orchestrations;

/// <summary>
/// E-commerce checkout orchestration.
/// Demonstrates parallel effects, aggregation, and complex workflows.
/// </summary>
public static class CheckoutOrchestration
{
    public static ReactiveGraph Build()
    {
        return OrchestrationBuilder.Create("checkout", "Checkout Flow", "1.0.0")
            // Shipping address fields
            .Field<string>("shippingStreet")
                .Default("")
                .Required()
                .BindTo("#shipping-street")
                .Add()

            .Field<string>("shippingCity")
                .Default("")
                .Required()
                .BindTo("#shipping-city")
                .Add()

            .Field<string>("shippingZip")
                .Default("")
                .Required()
                .Pattern(@"^\d{5}(-\d{4})?$", "Invalid ZIP code")
                .BindTo("#shipping-zip")
                .Add()

            .Field<string>("shippingCountry")
                .Default("US")
                .Required()
                .BindTo("#shipping-country")
                .Add()

            // Billing same as shipping toggle
            .Field<bool>("billingSameAsShipping")
                .Default(true)
                .BindTo("#billing-same")
                .Add()

            // Conditional billing address fields
            .When("showBillingAddress")
                .DependsOn("billingSameAsShipping")
                .Condition(ctx => !ctx.GetOrDefault<bool>(new("billingSameAsShipping"), true))
                .ThenActivate("billingStreet", "billingCity", "billingZip")
                .Add()

            .Field<string>("billingStreet")
                .Default("")
                .BindTo("#billing-street")
                .Add()

            .Field<string>("billingCity")
                .Default("")
                .BindTo("#billing-city")
                .Add()

            .Field<string>("billingZip")
                .Default("")
                .BindTo("#billing-zip")
                .Add()

            // Payment method selection
            .Field<string>("paymentMethod")
                .Default("card")
                .Required()
                .BindTo("#payment-method")
                .Add()

            // Card details (conditional on payment method)
            .When("showCardDetails")
                .DependsOn("paymentMethod")
                .Condition(ctx => ctx.GetOrDefault<string>(new("paymentMethod"), "") == "card")
                .ThenActivate("cardNumber", "cardExpiry", "cardCvv")
                .Add()

            .Field<string>("cardNumber")
                .Default("")
                .CreditCard()
                .BindTo("#card-number")
                .Add()

            .Field<string>("cardExpiry")
                .Default("")
                .Pattern(@"^\d{2}/\d{2}$", "Use MM/YY format")
                .BindTo("#card-expiry")
                .Add()

            .Field<string>("cardCvv")
                .Default("")
                .Pattern(@"^\d{3,4}$", "Invalid CVV")
                .BindTo("#card-cvv")
                .Add()

            // Cart total (simulated from external data)
            .Field<decimal>("cartTotal")
                .Default(0m)
                .Add()

            // Coupon code
            .Field<string>("couponCode")
                .Default("")
                .BindTo("#coupon-code")
                .Add()

            // Validate and apply coupon trigger
            .Trigger("applyCoupon")
                .OnClick()
                .BindTo("#apply-coupon-btn")
                .Debounce(TimeSpan.FromSeconds(1))
                .Add()

            // Coupon validation effect
            .Effect<CouponResult>("validateCoupon")
                .DependsOn("applyCoupon", "couponCode")
                .WithRetry(2)
                .WithTimeout(10)
                .ExecuteAsync(async ctx =>
                {
                    var code = ctx.GetOrDefault<string>(new("couponCode"), "");
                    if (string.IsNullOrEmpty(code))
                        return new CouponResult { IsValid = false };

                    // Simulate API call
                    await Task.Delay(500, ctx.CancellationToken);

                    // Demo: codes starting with "SAVE" are valid
                    if (code.StartsWith("SAVE", StringComparison.OrdinalIgnoreCase))
                    {
                        var discount = int.TryParse(code.Substring(4), out var d) ? d : 10;
                        return new CouponResult
                        {
                            IsValid = true,
                            DiscountPercent = Math.Min(discount, 50)
                        };
                    }

                    return new CouponResult { IsValid = false, ErrorMessage = "Invalid coupon code" };
                })
                .Add()

            // Calculate totals trigger
            .Trigger("calculateTotals")
                .OnChange()
                .BindTo("#shipping-zip, #coupon-code")
                .Debounce(TimeSpan.FromMilliseconds(500))
                .Add()

            // Parallel: fetch shipping rates, tax rates, and inventory check
            .Effect<ShippingRates>("fetchShippingRates")
                .DependsOn("calculateTotals", "shippingZip", "shippingCountry")
                .WithRetry(3)
                .ExecuteAsync(async ctx =>
                {
                    var zip = ctx.GetOrDefault<string>(new("shippingZip"), "");
                    await Task.Delay(300, ctx.CancellationToken);

                    return new ShippingRates
                    {
                        Standard = 5.99m,
                        Express = 12.99m,
                        Overnight = 24.99m
                    };
                })
                .Add()

            .Effect<TaxResult>("calculateTax")
                .DependsOn("calculateTotals", "shippingZip", "cartTotal")
                .WithRetry(3)
                .ExecuteAsync(async ctx =>
                {
                    var total = ctx.GetOrDefault<decimal>(new("cartTotal"), 0m);
                    await Task.Delay(200, ctx.CancellationToken);

                    // Simplified tax calculation
                    return new TaxResult { TaxAmount = total * 0.08m, TaxRate = 0.08m };
                })
                .Add()

            .Effect<InventoryResult>("checkInventory")
                .DependsOn("calculateTotals")
                .WithRetry(3)
                .ExecuteAsync(async ctx =>
                {
                    await Task.Delay(250, ctx.CancellationToken);
                    return new InventoryResult { AllInStock = true };
                })
                .Add()

            // Aggregate shipping, tax, and inventory results
            .Aggregate<object, OrderSummary>("orderSummary")
                .FromSources("fetchShippingRates", "calculateTax", "checkInventory", "validateCoupon")
                .WaitAll()
                .WithTimeout(TimeSpan.FromSeconds(10))
                .Aggregate(results =>
                {
                    var shipping = results.OfType<ShippingRates>().FirstOrDefault();
                    var tax = results.OfType<TaxResult>().FirstOrDefault();
                    var inventory = results.OfType<InventoryResult>().FirstOrDefault();
                    var coupon = results.OfType<CouponResult>().FirstOrDefault();

                    return new OrderSummary
                    {
                        ShippingOptions = shipping ?? new ShippingRates(),
                        Tax = tax ?? new TaxResult(),
                        InStock = inventory?.AllInStock ?? false,
                        DiscountPercent = coupon?.DiscountPercent ?? 0
                    };
                })
                .Add()

            // Computed: order total
            .Computed<decimal>("orderTotal")
                .DependsOn("cartTotal", "orderSummary")
                .Compute(ctx =>
                {
                    var cartTotal = ctx.GetOrDefault<decimal>(new("cartTotal"), 0m);
                    var summary = ctx.GetOrDefault<OrderSummary>(new("orderSummary"), new());

                    var discount = cartTotal * (summary.DiscountPercent / 100m);
                    var subtotal = cartTotal - discount;
                    var shipping = summary.ShippingOptions.Standard;
                    var tax = summary.Tax.TaxAmount;

                    return subtotal + shipping + tax;
                })
                .BindTo("#order-total")
                .Add()

            // Place order trigger
            .Trigger("placeOrder")
                .OnSubmit()
                .RequiresValid("shippingStreet", "shippingCity", "shippingZip", "paymentMethod")
                .Throttle(TimeSpan.FromSeconds(5))
                .BindTo("#place-order-btn")
                .Add()

            // Place order effect (sequential - must not run in parallel)
            .Effect<OrderResult>("submitOrder")
                .DependsOn("placeOrder", "shippingStreet", "shippingCity", "shippingZip",
                    "paymentMethod", "cardNumber", "orderTotal")
                .Sequential()
                .WithRetry(RetryPolicy.Aggressive)
                .WithTimeout(60)
                .WithIdempotencyKey(ctx =>
                {
                    var zip = ctx.GetOrDefault<string>(new("shippingZip"), "");
                    var total = ctx.GetOrDefault<decimal>(new("orderTotal"), 0m);
                    return $"order:{zip}:{total}:{DateTimeOffset.UtcNow.ToString("yyyyMMddHH")}";
                })
                .ExecuteAsync(async ctx =>
                {
                    ctx.Logger.Info("Submitting order...");

                    // Simulate order processing
                    ctx.ReportProgress(0.2, "Validating payment...");
                    await Task.Delay(1000, ctx.CancellationToken);

                    ctx.ReportProgress(0.5, "Processing payment...");
                    await Task.Delay(1000, ctx.CancellationToken);

                    ctx.ReportProgress(0.8, "Creating order...");
                    await Task.Delay(500, ctx.CancellationToken);

                    return new OrderResult
                    {
                        OrderId = $"ORD-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}",
                        Status = "confirmed",
                        EstimatedDelivery = DateTimeOffset.UtcNow.AddDays(5)
                    };
                })
                .Add()

            .Build();
    }

    // DTOs
    public record CouponResult
    {
        public bool IsValid { get; init; }
        public int DiscountPercent { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public record ShippingRates
    {
        public decimal Standard { get; init; }
        public decimal Express { get; init; }
        public decimal Overnight { get; init; }
    }

    public record TaxResult
    {
        public decimal TaxAmount { get; init; }
        public decimal TaxRate { get; init; }
    }

    public record InventoryResult
    {
        public bool AllInStock { get; init; }
    }

    public record OrderSummary
    {
        public ShippingRates ShippingOptions { get; init; } = new();
        public TaxResult Tax { get; init; } = new();
        public bool InStock { get; init; }
        public int DiscountPercent { get; init; }
    }

    public record OrderResult
    {
        public string OrderId { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTimeOffset EstimatedDelivery { get; init; }
    }
}
