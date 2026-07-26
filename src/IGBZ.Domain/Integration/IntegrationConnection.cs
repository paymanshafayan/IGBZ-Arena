using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Integration;

public enum IntegrationType
{
    Payment = 0,
    Shipping = 1,
    Marketplace = 2,
    Accounting = 3,
    Tax = 4,
    AI = 5,
    Ads = 6,
}

/// <summary>
/// ارتباطات یکپارچه‌سازی‌های بیرونی مستأجر (بخش ۸ - فاز ۶ و ۷).
/// </summary>
public sealed class IntegrationConnection : TenantEntity
{
    private readonly Dictionary<string, string> _settings = [];

    public IntegrationConnection(
        string id,
        TenantId tenantId,
        string providerName,
        IntegrationType type,
        string apiKey,
        IReadOnlyDictionary<string, string>? settings = null)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        ProviderName = providerName;
        Type = type;
        ApiKey = apiKey;

        if (settings is not null)
        {
            _settings = new Dictionary<string, string>(settings, StringComparer.Ordinal);
        }
    }

    /// <summary>سازنده بازسازی برای لایه ذخیره‌سازی.</summary>
    public IntegrationConnection(
        string id,
        string tenantId,
        string providerName,
        IntegrationType type,
        string apiKey,
        bool isConnected,
        IReadOnlyDictionary<string, string> settings,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? updatedAtUtc)
        : base(id, new TenantId(tenantId))
    {
        ProviderName = providerName;
        Type = type;
        ApiKey = apiKey;
        IsConnected = isConnected;
        _settings = new Dictionary<string, string>(settings, StringComparer.Ordinal);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string ProviderName { get; private set; }

    public IntegrationType Type { get; private set; }

    public string ApiKey { get; private set; }

    public bool IsConnected { get; private set; } = true;

    public IReadOnlyDictionary<string, string> Settings => _settings;

    public void Disconnect()
    {
        IsConnected = false;
        Touch();
    }
}
