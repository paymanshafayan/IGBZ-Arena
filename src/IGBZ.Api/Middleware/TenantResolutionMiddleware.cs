using IGBZ.Application.Abstractions;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Api.Middleware;

/// <summary>
/// استخراج مستأجر جاری (بخش ۴). ترتیب اولویت:
/// ۱) Claim با نام <c>tenant_id</c> در JWT (اپ‌های موبایل)
/// ۲) Host header / زیردامنه (وب — سایت مادر و فروشگاه‌ها)
/// هدر <c>X-Store-Id</c> فقط در محیط توسعه پذیرفته می‌شود؛ در Production قابل جعل است.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public const string TenantClaimType = "tenant_id";
    public const string DevOverrideHeader = "X-Store-Id";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IHostEnvironment environment)
    {
        var resolved = ResolveFromClaims(context)
            ?? ResolveFromHost(context)
            ?? ResolveFromDevHeader(context, environment);

        if (resolved is null)
        {
            logger.LogWarning(
                "Tenant could not be resolved for {Method} {Path} (host: {Host}).",
                context.Request.Method,
                context.Request.Path,
                context.Request.Host.Host);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "tenant_not_resolved",
                message = "Unable to determine the store for this request.",
            }).ConfigureAwait(false);
            return;
        }

        tenantContext.Set(resolved.Value);
        context.Items[TenantClaimType] = resolved.Value.Value;

        // ---- پایش سیستم و ساختار لاگین با تزریق شناسه مستأجر (فاز ۱ - بخش ۳) ----
        using (logger.BeginScope(new Dictionary<string, object> { { "tenantId", resolved.Value.Value } }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    private static TenantId? ResolveFromClaims(HttpContext context)
    {
        var claim = context.User.FindFirst(TenantClaimType)?.Value;
        return TryParse(claim);
    }

    private static TenantId? ResolveFromHost(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var options = context.RequestServices.GetService<PlatformHostOptions>() ?? PlatformHostOptions.Default;

        if (host.Equals(options.RootDomain, StringComparison.OrdinalIgnoreCase) ||
            host.Equals($"www.{options.RootDomain}", StringComparison.OrdinalIgnoreCase))
        {
            return TenantId.Platform;
        }

        var suffix = $".{options.RootDomain}";
        if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var label = host[..^suffix.Length];
            return label.Contains('.', StringComparison.Ordinal) ? null : TryParse(label);
        }

        // دامنهٔ اختصاصی: نگاشتش در فاز ۲ از platform_domain_mappings خوانده می‌شود.
        return null;
    }

    private static TenantId? ResolveFromDevHeader(HttpContext context, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return null;
        }

        return TryParse(context.Request.Headers[DevOverrideHeader].FirstOrDefault());
    }

    private static TenantId? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new TenantId(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public sealed record PlatformHostOptions(string RootDomain)
{
    public static readonly PlatformHostOptions Default = new("igbz.local");
}
