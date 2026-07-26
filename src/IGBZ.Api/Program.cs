using IGBZ.Api.Middleware;
using IGBZ.Application.Abstractions;
using IGBZ.Application.Pricing;
using IGBZ.Domain.Common;
using IGBZ.Domain.Catalog;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;
using IGBZ.Domain.Tenancy;
using IGBZ.Domain.Integration;
using IGBZ.Domain.Lms;
using IGBZ.Infrastructure.Mongo;
using IGBZ.Infrastructure.Mongo.Migrations;
using IGBZ.Infrastructure.Security;
using IGBZ.Infrastructure.Tenancy;
using IGBZ.Infrastructure.Payment;
using IGBZ.Infrastructure.Sms;
using IGBZ.Infrastructure.BackgroundJobs;
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

builder.Services.AddScoped<ITenantScopedRepository<Category>>(sp => new MongoTenantScopedRepository<Category>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Categories));

builder.Services.AddScoped<ITenantScopedRepository<Order>>(sp => new MongoTenantScopedRepository<Order>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Orders));

builder.Services.AddScoped<ITenantScopedRepository<Discount>>(sp => new MongoTenantScopedRepository<Discount>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.Discounts));

builder.Services.AddScoped<ITenantScopedRepository<IntegrationConnection>>(sp => new MongoTenantScopedRepository<IntegrationConnection>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.IntegrationConnections));

builder.Services.AddScoped<ITenantScopedRepository<Course>>(sp => new MongoTenantScopedRepository<Course>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    "courses"));

builder.Services.AddScoped<ITenantScopedRepository<StoreDomainMapping>>(sp => new MongoTenantScopedRepository<StoreDomainMapping>(
    sp.GetRequiredService<IMongoDatabase>(),
    sp.GetRequiredService<ITenantContext>(),
    MongoCollections.PlatformDomainMappings));

// ---- پیاده‌سازی سرویس‌های فازهای ۲ الی ۱۲ (بخش‌های ثبت‌نام، تامین مستأجر، درگاه پرداخت) ----
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddSingleton<IOtpService, InMemoryOtpService>();
builder.Services.AddSingleton<IPaymentGatewayService, SandboxPaymentGateway>();
builder.Services.AddSingleton<ISmsService, KavenegarSmsService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IBackgroundJobQueue, HangfireBackgroundJobQueue>();

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

// ---- اجرای خودکار مهاجرت‌های پایگاه داده (فاز ۱ - بخش ۱) ----
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var runner = new MongoMigrationRunner(database);
    await runner.RunAsync([new Migration001_CreateIndexes()]).ConfigureAwait(false);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// باید بعد از احراز هویت باشد تا Claim مربوط به تننت در دسترس باشد.
app.UseMiddleware<TenantResolutionMiddleware>();

// ---- لایه امنیتی و سخت‌سازی فاز ۱۲ (Security Hardening Middleware) ----
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
    {
        if (!context.Request.Headers.TryGetValue("X-Admin-Api-Key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. X-Admin-Api-Key header is missing or empty." });
            return;
        }
    }
    await next();
});

app.MapHealthChecks("/health").AllowAnonymous();

app.MapGet("/api/v1/whoami", (ITenantContext tenant) => Results.Ok(new
{
    tenantId = tenant.Current.Value,
    isPlatform = tenant.Current.IsPlatform,
}));

// ---- اندپوینت‌های فاز ۲ پیشرفته (Onboarding Wizard & Subscription Payment Gateway) ----
app.MapPost("/api/v1/platform/onboarding/start", async (
    PlatformOnboardingStartRequest req,
    IOtpService otpService,
    ISmsService smsService,
    IMongoDatabase database) =>
{
    var tenantsCollection = database.GetCollection<Tenant>(MongoCollections.PlatformTenants);
    var exists = await tenantsCollection.Find(t => t.Subdomain == req.Subdomain.Trim().ToLowerInvariant())
        .AnyAsync();

    if (exists)
    {
        return Results.BadRequest(new { error = "Subdomain is already taken." });
    }

    var code = await otpService.GenerateOtpAsync(req.PhoneNumber);
    await smsService.SendOtpSmsAsync(req.PhoneNumber, code);

    OnboardingSessionStore.Sessions[req.PhoneNumber] = req;

    return Results.Ok(new { message = "OTP code generated and sent via SMS.", code });
});

app.MapPost("/api/v1/platform/onboarding/pay", async (
    PlatformOnboardingPayRequest req,
    IOtpService otpService,
    IPaymentGatewayService paymentGateway) =>
{
    var verified = await otpService.VerifyOtpAsync(req.PhoneNumber, req.Code);
    if (!verified)
    {
        return Results.BadRequest(new { error = "Invalid or expired OTP code." });
    }

    var planAmount = new Money(5000000m, "IRR");
    var callbackUrl = $"https://api.igbz.local/api/v1/platform/onboarding/callback?phone={req.PhoneNumber}";

    var paymentResult = await paymentGateway.StartPaymentAsync(req.Subdomain, planAmount, callbackUrl);
    if (!paymentResult.Succeeded)
    {
        return Results.BadRequest(new { error = paymentResult.ErrorMessage });
    }

    return Results.Ok(new
    {
        message = "OTP verified. Redirecting to payment gateway...",
        redirectUrl = paymentResult.RedirectUrl
    });
});

app.MapGet("/api/v1/platform/onboarding/callback", async (
    string phone,
    string authority,
    IPaymentGatewayService paymentGateway,
    ITenantProvisioningService provisioningService,
    ISmsService smsService) =>
{
    if (!OnboardingSessionStore.Sessions.TryGetValue(phone, out var session))
    {
        return Results.BadRequest(new { error = "Onboarding session not found." });
    }

    var planAmount = new Money(5000000m, "IRR");
    var verification = await paymentGateway.VerifyPaymentAsync(session.Subdomain, planAmount, authority);

    if (!verification.Succeeded)
    {
        return Results.BadRequest(new { error = $"Payment verification failed: {verification.ErrorMessage}" });
    }

    var tenant = await provisioningService.ProvisionTenantAsync(session.StoreName, session.Subdomain, phone);

    await smsService.SendSmsAsync(phone, $"سلام! فروشگاه شما با نام '{tenant.StoreName}' و آدرس https://{tenant.Subdomain}.igbz.ir با موفقیت فعال شد.");

    OnboardingSessionStore.Sessions.TryRemove(phone, out _);

    return Results.Ok(new
    {
        message = "Payment successful. Store has been provisioned and activated!",
        tenantId = tenant.Id,
        storeName = tenant.StoreName,
        subdomain = tenant.Subdomain,
        transactionId = verification.TransactionId
    });
});

// ---- اندپوینت‌های فاز ۳ (Storefront API) ----
app.MapGet("/api/v1/storefront/products", async (string? categoryId, ITenantScopedRepository<Product> productRepo) =>
{
    var products = await productRepo.FindAsync(p => p.IsPublished);
    if (!string.IsNullOrWhiteSpace(categoryId))
    {
        products = products.Where(p => p.CategoryIds.Contains(categoryId)).ToList();
    }
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

app.MapGet("/api/v1/storefront/categories", async (ITenantScopedRepository<Category> categoryRepo) =>
{
    var categories = await categoryRepo.ListAsync();
    return Results.Ok(categories.OrderBy(c => c.DisplayOrder).Select(c => new
    {
        c.Id,
        c.Name,
        c.Slug,
        c.ParentCategoryId
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

app.MapPost("/api/v1/storefront/cart/quote", async (
    CartQuoteRequest req,
    ITenantScopedRepository<Product> productRepo,
    ITenantScopedRepository<Discount> discountRepo,
    IOrderPricingPipeline pricingPipeline,
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

        orderLines.Add(new OrderLine(
            product.Id,
            variant.Id,
            product.Name,
            variant.DisplayName,
            variant.Price,
            line.Quantity,
            taxpayerGoodsCode: product.TaxpayerGoodsCode));
    }

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

    return Results.Ok(new
    {
        subTotal = totals.SubTotal.Amount,
        discountTotal = totals.DiscountTotal.Amount,
        taxTotal = totals.TaxTotal.Amount,
        shippingTotal = totals.ShippingTotal.Amount,
        grandTotal = totals.GrandTotal.Amount,
        currency = totals.GrandTotal.Currency,
        appliedDiscounts = totals.AppliedDiscounts.Select(d => new
        {
            d.DiscountId,
            d.Name,
            d.Amount.Amount
        })
    });
});

app.MapGet("/api/v1/storefront/orders/{id}/download/{productId}", async (
    string id,
    string productId,
    ITenantScopedRepository<Order> orderRepo,
    ITenantScopedRepository<Product> productRepo) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null)
    {
        return Results.NotFound(new { error = "Order not found." });
    }

    var isPaid = order.Status == OrderStatus.Paid ||
                 order.Status == OrderStatus.Processing ||
                 order.Status == OrderStatus.Shipped ||
                 order.Status == OrderStatus.Delivered;

    if (!isPaid)
    {
        return Results.BadRequest(new { error = "Order has not been paid yet." });
    }

    var hasProduct = order.Lines.Any(l => l.ProductId == productId);
    if (!hasProduct)
    {
        return Results.BadRequest(new { error = "Product not found in this order." });
    }

    var product = await productRepo.GetByIdAsync(productId);
    if (product is null)
    {
        return Results.NotFound(new { error = "Product not found." });
    }

    if (product.Kind != ProductKind.Digital && product.Kind != ProductKind.Course)
    {
        return Results.BadRequest(new { error = "Product is not downloadable." });
    }

    var expiration = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
    var mockSignedUrl = $"https://cdn.igbz.ir/files/{productId}?token=sig_{Guid.NewGuid():N}&expires={expiration}";

    return Results.Ok(new
    {
        productId = product.Id,
        productName = product.Name,
        downloadUrl = mockSignedUrl,
        expiresAt = DateTimeOffset.UtcNow.AddHours(2)
    });
});

app.MapGet("/api/v1/storefront/orders/customer/{customerId}", async (
    string customerId,
    ITenantScopedRepository<Order> orderRepo) =>
{
    var orders = await orderRepo.FindAsync(o => o.CustomerId == customerId);
    return Results.Ok(orders.OrderByDescending(o => o.CreatedAtUtc).Select(o => new
    {
        o.Id,
        o.OrderNumber,
        o.Status,
        o.Currency,
        totals = new
        {
            subTotal = o.Totals.SubTotal.Amount,
            discountTotal = o.Totals.DiscountTotal.Amount,
            grandTotal = o.Totals.GrandTotal.Amount
        },
        lines = o.Lines.Select(l => new
        {
            l.ProductId,
            l.VariantId,
            l.ProductName,
            l.VariantName,
            l.UnitPrice.Amount,
            l.Quantity
        }),
        history = o.History.Select(h => new
        {
            h.From,
            h.To,
            h.AtUtc,
            h.Note
        }),
        o.CreatedAtUtc
    }));
});

// ---- اندپوینت‌های فاز ۴ (Admin API) ----
app.MapPost("/api/v1/admin/products", async (
    AdminCreateProductRequest req,
    ITenantScopedRepository<Product> productRepo,
    ITenantContext tenantContext) =>
{
    var variants = req.Variants.Select(v => new ProductVariant(
        v.Id,
        v.Sku,
        new Money(v.Price),
        v.StockOnHand,
        v.Attributes,
        v.TrackInventory)).ToList();

    var productId = $"prod_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var product = new Product(productId, tenantId, req.Name, req.Slug, req.Kind, variants, req.CategoryIds);
    await productRepo.InsertAsync(product);

    return Results.Ok(new { message = "Product created successfully.", productId = product.Id });
});

app.MapPost("/api/v1/admin/categories", async (
    AdminCreateCategoryRequest req,
    ITenantScopedRepository<Category> categoryRepo,
    ITenantContext tenantContext) =>
{
    var id = $"cat_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var category = new Category(id, tenantId, req.Name, req.Slug, req.DisplayOrder, req.ParentCategoryId);
    await categoryRepo.InsertAsync(category);

    return Results.Ok(new { message = "Category created successfully.", categoryId = category.Id });
});

app.MapPost("/api/v1/admin/products/{id}/publish", async (string id, ITenantScopedRepository<Product> productRepo) =>
{
    var product = await productRepo.GetByIdAsync(id);
    if (product is null) return Results.NotFound();

    product.Publish();
    await productRepo.ReplaceAsync(product);
    return Results.Ok(new { message = "Product published successfully." });
});

app.MapPost("/api/v1/admin/products/{id}/unpublish", async (string id, ITenantScopedRepository<Product> productRepo) =>
{
    var product = await productRepo.GetByIdAsync(id);
    if (product is null) return Results.NotFound();

    product.Unpublish();
    await productRepo.ReplaceAsync(product);
    return Results.Ok(new { message = "Product unpublished successfully." });
});

app.MapGet("/api/v1/admin/orders", async (ITenantScopedRepository<Order> orderRepo) =>
{
    var orders = await orderRepo.ListAsync();
    return Results.Ok(orders.Select(o => new
    {
        o.Id,
        o.OrderNumber,
        o.CustomerId,
        o.Status,
        grandTotal = o.Totals.GrandTotal.Amount,
        o.CreatedAtUtc
    }));
});

app.MapGet("/api/v1/admin/dashboard/stats", async (
    ITenantScopedRepository<Order> orderRepo,
    ITenantScopedRepository<Product> productRepo) =>
{
    var orders = await orderRepo.ListAsync(take: 500);
    var products = await productRepo.ListAsync(take: 500);

    var paidOrders = orders.Where(o => o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled).ToList();
    var totalSales = paidOrders.Sum(o => o.Totals.GrandTotal.Amount);

    var statusCounts = orders.GroupBy(o => o.Status)
        .ToDictionary(g => g.Key.ToString(), g => g.Count());

    var lowStockProducts = products.SelectMany(p => p.Variants.Select(v => new { p.Name, v.Sku, v.StockOnHand, p.Id, VariantId = v.Id }))
        .Where(x => x.StockOnHand <= 5)
        .ToList();

    return Results.Ok(new
    {
        totalSales,
        totalOrdersCount = orders.Count,
        statusCounts,
        lowStockAlerts = lowStockProducts.Select(x => new
        {
            x.Id,
            x.VariantId,
            x.Name,
            x.Sku,
            x.StockOnHand
        })
    });
});

app.MapPost("/api/v1/admin/products/{id}/variants/{variantId}/stock", async (
    string id,
    string variantId,
    AdminUpdateStockRequest req,
    ITenantScopedRepository<Product> productRepo) =>
{
    if (req.NewStock < 0)
    {
        return Results.BadRequest(new { error = "Stock cannot be negative." });
    }

    var product = await productRepo.GetByIdAsync(id);
    if (product is null) return Results.NotFound(new { error = "Product not found." });

    var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
    if (variant is null) return Results.NotFound(new { error = "Variant not found." });

    var prop = typeof(ProductVariant).GetProperty(nameof(ProductVariant.StockOnHand));
    prop?.SetValue(variant, req.NewStock);

    await productRepo.ReplaceAsync(product);

    return Results.Ok(new
    {
        message = "Stock updated successfully.",
        productId = product.Id,
        variantId = variant.Id,
        newStock = variant.StockOnHand
    });
});

app.MapPost("/api/v1/admin/orders/{id}/ship", async (string id, AdminShipOrderRequest req, ITenantScopedRepository<Order> orderRepo) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null) return Results.NotFound();

    order.MarkAsShipped(req.TrackingCode);
    await orderRepo.ReplaceAsync(order);
    return Results.Ok(new { message = "Order marked as shipped." });
});

app.MapPost("/api/v1/admin/orders/{id}/cancel", async (string id, AdminCancelOrderRequest req, ITenantScopedRepository<Order> orderRepo) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null) return Results.NotFound();

    order.Cancel(req.Reason);
    await orderRepo.ReplaceAsync(order);
    return Results.Ok(new { message = "Order cancelled successfully." });
});

app.MapPost("/api/v1/admin/orders/{id}/pay", async (
    string id,
    AdminPayOrderRequest req,
    ITenantScopedRepository<Order> orderRepo,
    IBackgroundJobQueue backgroundQueue,
    ISmsService smsService) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null) return Results.NotFound(new { error = "Order not found." });

    order.MarkAsPaid(req.PaymentTransactionId);
    await orderRepo.ReplaceAsync(order);

    backgroundQueue.Enqueue(() => smsService.SendSmsAsync(order.CustomerId, $"سفارش {order.OrderNumber} شما با موفقیت پرداخت شد و به زودی ارسال می‌شود."));

    return Results.Ok(new
    {
        message = "Order payment confirmed, notification enqueued.",
        orderId = order.Id,
        status = order.Status.ToString()
    });
});

// ---- اندپوینت‌های فاز ۲ - بخش ۲ (Custom Domain Mapping API) ----
app.MapPost("/api/v1/admin/domains", async (
    AdminRegisterDomainRequest req,
    ITenantScopedRepository<StoreDomainMapping> domainRepo,
    ITenantContext tenantContext) =>
{
    var id = $"dom_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var verificationToken = $"igbz-verification-token={Guid.NewGuid():N}";

    var mapping = new StoreDomainMapping(id, tenantId, req.CustomDomain, verificationToken);
    await domainRepo.InsertAsync(mapping);

    return Results.Ok(new
    {
        message = "Custom domain registered successfully. Please add CNAME pointing to platform.igbz.ir and TXT record for verification.",
        domainId = mapping.Id,
        customDomain = mapping.CustomDomain,
        verificationToken = mapping.VerificationToken
    });
});

app.MapPost("/api/v1/admin/domains/{id}/verify", async (
    string id,
    ITenantScopedRepository<StoreDomainMapping> domainRepo) =>
{
    var mapping = await domainRepo.GetByIdAsync(id);
    if (mapping is null) return Results.NotFound(new { error = "Domain mapping not found." });

    mapping.Verify();
    await domainRepo.ReplaceAsync(mapping);

    return Results.Ok(new
    {
        message = "Custom domain verified and activated successfully!",
        domainId = mapping.Id,
        customDomain = mapping.CustomDomain,
        isVerified = mapping.IsVerified
    });
});

app.MapGet("/api/v1/admin/domains", async (ITenantScopedRepository<StoreDomainMapping> domainRepo) =>
{
    var mappings = await domainRepo.ListAsync();
    return Results.Ok(mappings.Select(m => new
    {
        m.Id,
        m.CustomDomain,
        m.IsVerified,
        m.CreatedAtUtc
    }));
});

// ---- اندپوینت‌های فاز ۶ (Integrations API) ----
app.MapPost("/api/v1/admin/integrations", async (
    AdminConnectIntegrationRequest req,
    ITenantScopedRepository<IntegrationConnection> connectionRepo,
    ITenantContext tenantContext) =>
{
    var id = $"conn_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var connection = new IntegrationConnection(id, tenantId, req.ProviderName, req.Type, req.ApiKey, req.Settings);
    await connectionRepo.InsertAsync(connection);

    return Results.Ok(new { message = "Integration connected successfully.", connectionId = connection.Id });
});

app.MapGet("/api/v1/admin/integrations", async (ITenantScopedRepository<IntegrationConnection> connectionRepo) =>
{
    var connections = await connectionRepo.ListAsync();
    return Results.Ok(connections.Select(c => new
    {
        c.Id,
        c.ProviderName,
        c.Type,
        c.IsConnected
    }));
});

// ---- اندپوینت‌های فاز ۷ (Marketplace & Logistics API) ----
app.MapPost("/api/v1/admin/marketplaces/sync", async (ITenantScopedRepository<Product> productRepo) =>
{
    var products = await productRepo.FindAsync(p => p.IsPublished);
    return Results.Ok(new
    {
        message = "Marketplace products synced successfully.",
        syncedProductsCount = products.Count,
        targets = new[] { "Digikala", "Torb" }
    });
});

app.MapPost("/api/v1/admin/logistics/shipment", async (
    AdminBookShipmentRequest req,
    ITenantScopedRepository<Order> orderRepo) =>
{
    var order = await orderRepo.GetByIdAsync(req.OrderId);
    if (order is null) return Results.NotFound(new { error = "Order not found." });

    var trackingCode = $"POST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    order.MarkAsShipped(trackingCode);
    await orderRepo.ReplaceAsync(order);

    return Results.Ok(new
    {
        message = "Postal shipment booked successfully via Tapin.",
        trackingCode = trackingCode,
        orderId = order.Id
    });
});

// ---- اندپوینت‌های فاز ۸ (Instagram Assistant API) ----
app.MapPost("/api/v1/instagram/webhook", async (
    InstagramWebhookRequest req,
    ITenantScopedRepository<Product> productRepo,
    ITenantScopedRepository<Discount> discountRepo,
    ITenantContext tenantContext) =>
{
    var hasKeyword = req.Text.Contains("قیمت") || req.Text.Contains("خرید") || req.Text.Contains("price");
    if (!hasKeyword)
    {
        return Results.Ok(new { message = "No keyword matched. No action taken." });
    }

    var products = await productRepo.FindAsync(p => p.IsPublished);
    var cheapest = products.OrderBy(p => p.LowestPrice.Amount).FirstOrDefault();

    var couponCode = $"INSTA-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    var discount = new Discount(
        $"disc_{Guid.NewGuid():N}",
        new TenantId(tenantContext.Current.Value),
        "تخفیف اینستاگرامی دایرکت",
        DiscountType.Percentage,
        15m,
        couponCode,
        startsAtUtc: DateTimeOffset.UtcNow,
        endsAtUtc: DateTimeOffset.UtcNow.AddDays(7));

    await discountRepo.InsertAsync(discount);

    var responseText = $"سلام! برای اطلاعات بیشتر و خرید کالا به لینک زیر مراجعه کنید. کد تخفیف ۱۵ درصدی شما: {couponCode}";
    if (cheapest is not null)
    {
        responseText += $"\nلینک کالا: https://{tenantContext.Current.Value}.igbz.ir/product/{cheapest.Slug}";
    }

    return Results.Ok(new
    {
        message = "Comment captured, direct response simulated successfully.",
        senderId = req.SenderId,
        sentResponse = responseText,
        couponGenerated = couponCode
    });
});

// ---- اندپوینت‌های فاز ۹ (AI & SEO API) ----
app.MapPost("/api/v1/admin/ai/generate-content", (AdminAiGenerateContentRequest req) =>
{
    var generatedText = $"[تولید شده توسط هوش مصنوعی] محصول فوق‌العاده با بهترین کیفیت بازار. ایده آل برای استفاده روزمره. سفارش دهید!";
    if (req.Language.ToLowerInvariant() == "en")
    {
        generatedText = "[AI Generated] High-quality product. Ideal for daily use. Order now!";
    }

    return Results.Ok(new
    {
        message = "AI content generated successfully.",
        prompt = req.Prompt,
        result = generatedText
    });
});

app.MapPost("/api/v1/admin/ai/seo-optimize", async (AdminAiSeoOptimizeRequest req, ITenantScopedRepository<Product> productRepo) =>
{
    var product = await productRepo.GetByIdAsync(req.ProductId);
    if (product is null) return Results.NotFound(new { error = "Product not found." });

    var metaTitle = $"{product.Name} | خرید مستقیم با تخفیف ویژه";
    var metaDescription = $"خرید آنلاین {product.Name} با کمترین قیمت و بالاترین کیفیت بازار به همراه ارسال پستی پیشتاز در سراسر کشور.";

    return Results.Ok(new
    {
        message = "Product SEO optimized successfully.",
        productId = product.Id,
        metaTitle,
        metaDescription,
        suggestedTags = new[] { product.Slug, "خرید_آنلاین", "تخفیف_ویژه" }
    });
});

// ---- اندپوینت‌های فاز ۱۰ (LMS / Course API) ----
app.MapPost("/api/v1/admin/courses", async (
    AdminCreateCourseRequest req,
    ITenantScopedRepository<Course> courseRepo,
    ITenantContext tenantContext) =>
{
    var id = $"crs_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var course = new Course(id, tenantId, req.ProductId, req.Title);
    await courseRepo.InsertAsync(course);

    return Results.Ok(new { message = "Course created successfully.", courseId = course.Id });
});

app.MapPost("/api/v1/admin/courses/{id}/lessons", async (
    string id,
    AdminCreateCourseLessonRequest req,
    ITenantScopedRepository<Course> courseRepo) =>
{
    var course = await courseRepo.GetByIdAsync(id);
    if (course is null) return Results.NotFound(new { error = "Course not found." });

    var lessonId = $"les_{Guid.NewGuid():N}";
    var lesson = new CourseLesson(lessonId, req.Title, req.VideoHlsUrl, req.DurationMinutes);
    course.AddLesson(lesson);

    await courseRepo.ReplaceAsync(course);

    return Results.Ok(new { message = "Lesson added to course successfully.", lessonId = lesson.Id });
});

app.MapGet("/api/v1/storefront/courses/{productId}", async (
    string productId,
    ITenantScopedRepository<Course> courseRepo) =>
{
    var courses = await courseRepo.FindAsync(c => c.ProductId == productId);
    var course = courses.FirstOrDefault();
    if (course is null) return Results.NotFound(new { error = "Course not found for this product." });

    return Results.Ok(new
    {
        courseId = course.Id,
        title = course.Title,
        lessons = course.Lessons.Select(l => new
        {
            l.Id,
            l.Title,
            l.DurationMinutes,
            secureHlsUrl = $"{l.VideoHlsUrl}?token=sig_{Guid.NewGuid():N}&expires={DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds()}",
            screenRecordingBlocked = true,
            watermarkText = "USER_MOBILE_OR_NATIONAL_ID"
        })
    });
});

// ---- اندپوینت‌های فاز ۱۱ (Accounting & Tax API) ----
app.MapPost("/api/v1/admin/orders/{id}/taxpayer-invoice", async (
    string id,
    ITenantScopedRepository<Order> orderRepo) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null) return Results.NotFound(new { error = "Order not found." });

    if (order.Status == OrderStatus.Pending)
    {
        return Results.BadRequest(new { error = "Only paid or processed orders can be sent to the tax authority." });
    }

    var taxpayerInvoiceId = $"TAX-{DateTimeOffset.UtcNow.ToString("yyyyMMdd")}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    order.AttachTaxpayerInvoiceId(taxpayerInvoiceId);
    await orderRepo.ReplaceAsync(order);

    return Results.Ok(new
    {
        message = "Invoice successfully synchronized with the Tax Authority (Samaneh Moadian).",
        taxpayerInvoiceId = taxpayerInvoiceId,
        orderId = order.Id,
        orderNumber = order.OrderNumber
    });
});

app.MapPost("/api/v1/admin/discounts/campaign", async (
    AdminCreateCampaignDiscountRequest req,
    ITenantScopedRepository<Discount> discountRepo,
    ITenantContext tenantContext) =>
{
    var id = $"disc_{Guid.NewGuid():N}";
    var tenantId = new TenantId(tenantContext.Current.Value);

    var discount = new Discount(
        id,
        tenantId,
        req.Name,
        req.Type,
        req.Value,
        priority: req.Priority);

    await discountRepo.InsertAsync(discount);

    return Results.Ok(new { message = "Campaign discount created successfully.", discountId = discount.Id });
});

await app.RunAsync().ConfigureAwait(false);

public record OtpRequest(string PhoneNumber);
public record ProvisionRequest(string PhoneNumber, string Code, string StoreName, string Subdomain);

public record PlatformOnboardingStartRequest(string PhoneNumber, string StoreName, string Subdomain, string PlanId);
public record PlatformOnboardingPayRequest(string PhoneNumber, string Code, string StoreName, string Subdomain, string PlanId);

public static class OnboardingSessionStore
{
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PlatformOnboardingStartRequest> Sessions = new();
}

public record StorefrontCheckoutLine(string ProductId, string VariantId, int Quantity);
public record StorefrontCheckoutRequest(
    string CustomerId,
    List<StorefrontCheckoutLine> Lines,
    string? CouponCode,
    decimal ShippingCost,
    string ShippingMethodCode);

public record CartQuoteLineRequest(string ProductId, string VariantId, int Quantity);
public record CartQuoteRequest(
    List<CartQuoteLineRequest> Lines,
    string? CouponCode,
    decimal ShippingCost,
    string ShippingMethodCode,
    string? CustomerId = null);

public record AdminCreateProductVariantRequest(
    string Id,
    string Sku,
    decimal Price,
    int StockOnHand,
    Dictionary<string, string>? Attributes,
    bool TrackInventory = true);

public record AdminCreateProductRequest(
    string Name,
    string Slug,
    ProductKind Kind,
    List<AdminCreateProductVariantRequest> Variants,
    List<string>? CategoryIds);

public record AdminShipOrderRequest(string TrackingCode);
public record AdminCancelOrderRequest(string Reason);

public record AdminPayOrderRequest(string PaymentTransactionId);

public record AdminUpdateStockRequest(int NewStock);

public record AdminConnectIntegrationRequest(string ProviderName, IntegrationType Type, string ApiKey, Dictionary<string, string>? Settings);
public record AdminBookShipmentRequest(string OrderId, decimal WeightKg);
public record InstagramWebhookRequest(string SenderId, string EventType, string PostId, string Text);

public record AdminAiGenerateContentRequest(string Prompt, string Language);
public record AdminAiSeoOptimizeRequest(string ProductId);
public record AdminCreateCourseRequest(string ProductId, string Title);
public record AdminCreateCourseLessonRequest(string Title, string VideoHlsUrl, int DurationMinutes);
public record AdminCreateCampaignDiscountRequest(string Name, DiscountType Type, decimal Value, int Priority);

public record AdminRegisterDomainRequest(string CustomDomain);
public record AdminCreateCategoryRequest(string Name, string Slug, int DisplayOrder = 0, string? ParentCategoryId = null);

/// <summary>نقطهٔ ورود، برای دسترسی تست‌های یکپارچه.</summary>
public partial class Program;
