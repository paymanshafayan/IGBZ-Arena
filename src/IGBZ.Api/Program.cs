using IGBZ.Api.Middleware;
using IGBZ.Application.Abstractions;
using IGBZ.Application.Pricing;
using IGBZ.Domain.Common;
using IGBZ.Domain.Catalog;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;
using IGBZ.Domain.Tenancy;
using IGBZ.Infrastructure.Mongo;
using IGBZ.Infrastructure.Security;
using IGBZ.Infrastructure.Tenancy;
using IGBZ.Infrastructure.Payment;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ---- تننت (بخش ۴) ----
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddSingleton(new PlatformHostOptions(
    builder.Configuration["Platform:RootDomain"] ?? PlatformHostOptions.Default.RootDomain));

// ---- MongoDB ----
var mongoConnection = builder.Configuration.GetConnectionString("Mongo")
    ?? "mongodb://localhost:27017/?replicaSet=rs0";
var mongoDatabase = builder.Configuration["Mongo:Database"] ?? "igbz";

BsonConfiguration.Register();

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabase));
builder.Services.AddScoped<IInventoryService>(sp => new MongoInventoryService(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>()));
builder.Services.AddScoped<ICheckoutService>(sp => new MongoCheckoutService(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>()));

// ---- ثبت ریپازیتوری‌های اختصاصی تننت (فاز ۳) ----
builder.Services.AddScoped<ITenantScopedRepository<Product>>(sp => new MongoTenantScopedRepository<Product>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Products));

builder.Services.AddScoped<ITenantScopedRepository<Order>>(sp => new MongoTenantScopedRepository<Order>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Orders));

builder.Services.AddScoped<ITenantScopedRepository<Discount>>(sp => new MongoTenantScopedRepository<Discount>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Discounts));

// ---- پیاده‌سازی سرویس‌های فازهای ۲ الی ۱۲ (بخش‌های ثبت‌نام، تامین مستأجر، درگاه پرداخت) ----
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddSingleton<IOtpService, InMemoryOtpService>();
builder.Services.AddSingleton<IPaymentGatewayService, SandboxPaymentGateway>();

// ---- پایپ‌لاین قیمت‌گذاری (بخش ۵.۲) ----
builder.Services.AddSingleton<ISubTotalCalculator, SubTotalCalculator>();
builder.Services.AddSingleton<IDiscountCalculator, BestSingleDiscountCalculator>();
builder.Services.AddSingleton<ITaxCalculator, FlatVatCalculator>();
builder.Services.AddSingleton<IShippingCalculator, QuoteShippingCalculator>();
builder.Services.AddSingleton<IOrderPricingPipeline, OrderPricingPipeline>();

// احراز هویت در فاز ۲ (JWT) تکمیل می‌شود؛ فعلاً فقط سرویس‌های پایه ثبت می‌شوند
// تا UseAuthentication/UseAuthorization خطا ندهند.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// باید بعد از احراز هویت باشد تا Claim مربوط به تننت در دسترس باشد.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapGet("/api/v1/whoami", (ITenantContext tenant) => Results.Ok(new
{
    tenantId = tenant.Current.Value,
    isPlatform = tenant.Current.IsPlatform,
}));

// ---- اندپوینت‌های فاز ۲ (Onboarding & Provisioning) ----
app.MapPost("/api/v1/onboarding/otp/request", async (OtpRequest req, IOtpService otpService) =>
{
    var code = await otpService.GenerateOtpAsync(req.PhoneNumber);
    return Results.Ok(new { message = "OTP sent successfully.", code });
});

app.MapPost("/api/v1/onboarding/verify", async (ProvisionRequest req, IOtpService otpService, ITenantProvisioningService provisioningService) =>
{
    var verified = await otpService.VerifyOtpAsync(req.PhoneNumber, req.Code);
    if (!verified)
    {
        return Results.BadRequest(new { error = "Invalid or expired OTP." });
    }

    var tenant = await provisioningService.ProvisionTenantAsync(req.StoreName, req.Subdomain, req.PhoneNumber);
    return Results.Ok(new
    {
        message = "Tenant successfully provisioned.",
        tenantId = tenant.Id,
        storeName = tenant.StoreName,
        subdomain = tenant.Subdomain,
        status = tenant.Status.ToString()
    });
});

// ---- اندپوینت‌های فاز ۳ (Storefront API) ----
app.MapGet("/api/v1/storefront/products", async (ITenantScopedRepository<Product> productRepo) =>
{
    var products = await productRepo.FindAsync(p => p.IsPublished);
    return Results.Ok(products.Select(p => new
    {
        p.Id,
        p.Name,
        p.Slug,
        p.Kind,
        p.LowestPrice,
        Variants = p.Variants.Select(v => new
        {
            v.Id,
            v.Sku,
            v.Price,
            v.StockOnHand,
            v.TrackInventory,
            v.DisplayName
        })
    }));
});

app.MapGet("/api/v1/storefront/products/{slug}", async (string slug, ITenantScopedRepository<Product> productRepo) =>
{
    var products = await productRepo.FindAsync(p => p.Slug == slug.ToLowerInvariant() && p.IsPublished);
    var product = products.FirstOrDefault();
    if (product is null)
    {
        return Results.NotFound(new { error = "Product not found." });
    }

    return Results.Ok(new
    {
        product.Id,
        product.Name,
        product.Slug,
        product.Kind,
        product.LowestPrice,
        Variants = product.Variants.Select(v => new
        {
            v.Id,
            v.Sku,
            v.Price,
            v.StockOnHand,
            v.TrackInventory,
            v.DisplayName
        })
    });
});

app.MapPost("/api/v1/storefront/checkout", async (
    StorefrontCheckoutRequest req,
    ITenantScopedRepository<Product> productRepo,
    ITenantScopedRepository<Discount> discountRepo,
    IOrderPricingPipeline pricingPipeline,
    ICheckoutService checkoutService,
    ITenantContext tenantContext) =>
{
    if (req.Lines is null || req.Lines.Count == 0)
    {
        return Results.BadRequest(new { error = "Cart is empty." });
    }

    var orderLines = new List<OrderLine>();
    foreach (var line in req.Lines)
    {
        var product = await productRepo.GetByIdAsync(line.ProductId);
        if (product is null)
        {
            return Results.BadRequest(new { error = $"Product '{line.ProductId}' not found." });
        }

        var variant = product.Variants.FirstOrDefault(v => v.Id == line.VariantId);
        if (variant is null)
        {
            return Results.BadRequest(new { error = $"Variant '{line.VariantId}' not found on product '{line.ProductId}'." });
        }

        if (!variant.IsAvailable(line.Quantity))
        {
            return Results.BadRequest(new { error = $"Insufficient stock for variant '{variant.Sku}'." });
        }

        orderLines.Add(new OrderLine(
            product.Id,
            variant.Id,
            product.Name,
            variant.DisplayName,
            variant.Price,
            line.Quantity,
            taxpayerGoodsCode: product.TaxpayerGoodsCode));
    }

    var orderId = $"ord_{Guid.NewGuid():N}";
    var orderNumber = $"O-{DateTimeOffset.UtcNow.Ticks}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var order = new Order(orderId, tenantId, orderNumber, req.CustomerId, orderLines);

    var activeDiscounts = await discountRepo.FindAsync(d => d.IsActive);
    var shippingQuote = new ShippingQuote(new Money(req.ShippingCost), req.ShippingMethodCode);
    var pricingRequest = new PricingRequest(
        orderLines,
        activeDiscounts,
        req.CouponCode,
        shippingQuote,
        TaxSettings.DefaultIran,
        DateTimeOffset.UtcNow,
        CustomerId: req.CustomerId);

    var totals = pricingPipeline.Calculate(pricingRequest);
    order.ApplyTotals(totals);

    var checkoutResult = await checkoutService.CheckoutAsync(order);
    if (!checkoutResult.Succeeded)
    {
        return Results.BadRequest(new
        {
            error = checkoutResult.ErrorMessage,
            shortages = checkoutResult.Shortages.Select(s => new { s.ProductId, s.VariantId, s.Requested, s.Available })
        });
    }

    return Results.Ok(new
    {
        message = "Order placed successfully.",
        orderId = order.Id,
        orderNumber = order.OrderNumber,
        grandTotal = order.Totals.GrandTotal.Amount,
        currency = order.Totals.GrandTotal.Currency
    });
});

await app.RunAsync().ConfigureAwait(false);

public record OtpRequest(string PhoneNumber);
public record ProvisionRequest(string PhoneNumber, string Code, string StoreName, string Subdomain);

public record StorefrontCheckoutLine(string ProductId, string VariantId, int Quantity);
public record StorefrontCheckoutRequest(
    string CustomerId,
    List<StorefrontCheckoutLine> Lines,
    string? CouponCode,
    decimal ShippingCost,
    string ShippingMethodCode);

/// <summary>نقطهٔ ورود، برای دسترسی تست‌های یکپارچه.</summary>
public partial class Program;
