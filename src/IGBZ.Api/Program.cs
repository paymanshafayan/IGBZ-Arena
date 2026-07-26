using IGBZ.Api.Middleware;
using IGBZ.Application.Abstractions;
using IGBZ.Application.Pricing;
using IGBZ.Infrastructure.Mongo;
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

await app.RunAsync().ConfigureAwait(false);

/// <summary>نقطهٔ ورود، برای دسترسی تست‌های یکپارچه.</summary>
public partial class Program;
