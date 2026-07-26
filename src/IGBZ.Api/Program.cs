using IGBZ.Api.Middleware;
using IGBZ.Application.Abstractions;
using IGBZ.Application.Pricing;
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

await app.RunAsync().ConfigureAwait(false);

public record OtpRequest(string PhoneNumber);
public record ProvisionRequest(string PhoneNumber, string Code, string StoreName, string Subdomain);

/// <summary>نقطهٔ ورود، برای دسترسی تست‌های یکپارچه.</summary>
public partial class Program;
