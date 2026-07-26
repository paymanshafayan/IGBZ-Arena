# IGBZ

پلتفرم بومی «دستیار کسب‌وکارهای اینستاگرامی»: فروشگاه‌ساز چندمستأجری + اپ‌های Flutter +
دستیار هوشمند اینستاگرام.

سند مرجع معماری: [`ARCHITECTURE.md`](ARCHITECTURE.md) — منبع حقیقت پروژه.
تصمیمات معماری: [`docs/adr/`](docs/adr/)

## وضعیت

| فاز | عنوان | وضعیت |
|---|---|---|
| 0 | سند معماری و تصمیمات پایه | ✅ تکمیل |
| 1 | هستهٔ موتور تجارت + چندمستأجری | 🟡 در حال انجام — اسکلت، دامنه، پایپ‌لاین و تست‌ها آماده |
| 2+ | سایت مادر، Storefront، اپ‌ها … | ⏳ |

## استک

.NET 9 / C# · MongoDB (Replica Set) · Headless API · Next.js (فاز ۲ به بعد) · Flutter

## ساختار

```
src/
  IGBZ.Domain/          موجودیت‌ها، Order Aggregate، ماشین‌حالت، Money، تخفیف
  IGBZ.Application/     اینترفیس‌ها، پایپ‌لاین قیمت‌گذاری، TenantContext
  IGBZ.Infrastructure/  MongoDB، ریپازیتوری محدود به تننت، رزرو اتمی موجودی
  IGBZ.Api/             Web API واحد + Middleware تشخیص تننت
tests/
  IGBZ.Domain.Tests/        ماشین‌حالت، Money، TenantId
  IGBZ.Application.Tests/   پایپ‌لاین قیمت‌گذاری، جداسازی چندمستأجری
```

## اجرای محلی

```bash
# ۱) MongoDB به‌صورت Replica Set (برای تراکنش رزرو موجودی الزامی است)
docker compose -f docker-compose.dev.yml up -d

# ۲) بیلد و تست
dotnet restore IGBZ.sln
dotnet build   IGBZ.sln -c Release
dotnet test    IGBZ.sln -c Release

# ۳) اجرای API
dotnet run --project src/IGBZ.Api
```

### تشخیص فروشگاه در محیط توسعه

`Middleware` به‌ترتیب از JWT Claim (`tenant_id`)، سپس زیردامنه و در نهایت — **فقط در
Development** — از هدر `X-Store-Id` مستأجر را تشخیص می‌دهد:

```bash
curl -H "X-Store-Id: shop-a" http://localhost:5000/api/v1/whoami
```

## قواعد غیرقابل‌مذاکره

- هیچ کدی از nopCommerce کپی نمی‌شود (لایسنس NPL) — فقط مرجع منطق.
- هر Query روی داده‌های تننت باید از `ITenantScopedRepository` عبور کند.
- وضعیت سفارش فقط با متدهای صریح Aggregate تغییر می‌کند.
- مبالغ همیشه `decimal`/`Money` — هرگز `double`.
