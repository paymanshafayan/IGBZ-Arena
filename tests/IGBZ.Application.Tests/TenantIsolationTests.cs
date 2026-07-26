using System.Collections.Concurrent;
using FluentAssertions;
using IGBZ.Application.Abstractions;
using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Tenancy;
using IGBZ.Infrastructure.Tenancy;
using Xunit;

namespace IGBZ.Application.Tests;

/// <summary>
/// تست‌های تضمینی جداسازی چندمستأجری (بخش ۴).
/// این‌ها قرارداد امنیتی پلتفرم‌اند: هر شکستی اینجا یعنی نشت داده بین فروشگاه‌ها.
/// طبق بازبینی معماری، این تست‌ها بخشی از فاز ۱ هستند، نه فاز ۱۲.
/// </summary>
public class TenantIsolationTests
{
    private readonly ConcurrentDictionary<string, Order> _sharedStore = new();

    private ITenantScopedRepository<Order> RepoFor(string tenantId)
    {
        var context = new TenantContext();
        context.Set(new TenantId(tenantId));
        return new InMemoryTenantScopedRepository<Order>(_sharedStore, context);
    }

    private static Order OrderFor(string tenantId, string id) => new(
        id,
        new TenantId(tenantId),
        $"NUM-{id}",
        "cust-1",
        [new OrderLine("p1", "v1", "کالا", null, new Money(100_000m), 1)]);

    [Fact]
    public async Task Tenant_cannot_read_another_tenants_document_by_id()
    {
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "ord-1"));

        (await RepoFor("shop-a").GetByIdAsync("ord-1")).Should().NotBeNull();
        (await RepoFor("shop-b").GetByIdAsync("ord-1")).Should().BeNull();
    }

    [Fact]
    public async Task List_and_count_never_leak_across_tenants()
    {
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "a1"));
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "a2"));
        await RepoFor("shop-b").InsertAsync(OrderFor("shop-b", "b1"));

        (await RepoFor("shop-a").ListAsync()).Should().HaveCount(2);
        (await RepoFor("shop-b").ListAsync()).Should().HaveCount(1);
        (await RepoFor("shop-c").ListAsync()).Should().BeEmpty();

        (await RepoFor("shop-a").CountAsync()).Should().Be(2);
        (await RepoFor("shop-c").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Find_with_a_broad_predicate_still_respects_tenant_scope()
    {
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "a1"));
        await RepoFor("shop-b").InsertAsync(OrderFor("shop-b", "b1"));

        // حتی با پردیکیت «همه چیز درست است» نباید سند مستأجر دیگر برگردد.
        var results = await RepoFor("shop-b").FindAsync(_ => true);

        results.Should().ContainSingle();
        results[0].TenantId.Should().Be("shop-b");
    }

    [Fact]
    public async Task Writing_an_entity_of_another_tenant_is_rejected()
    {
        var repo = RepoFor("shop-b");
        var foreign = OrderFor("shop-a", "ord-x");

        var act = async () => await repo.InsertAsync(foreign);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task Delete_and_replace_cannot_touch_another_tenants_document()
    {
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "ord-1"));

        (await RepoFor("shop-b").DeleteAsync("ord-1")).Should().BeFalse();
        (await RepoFor("shop-a").GetByIdAsync("ord-1")).Should().NotBeNull();
    }

    [Fact]
    public void Unresolved_tenant_context_fails_loudly()
    {
        var context = new TenantContext();
        context.IsResolved.Should().BeFalse();

        var act = () => context.Current;
        act.Should().Throw<TenantNotResolvedException>();
    }

    [Fact]
    public void Tenant_context_cannot_be_reassigned_mid_request()
    {
        var context = new TenantContext();
        context.Set(new TenantId("shop-a"));

        var act = () => context.Set(new TenantId("shop-b"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Platform_pseudo_tenant_is_isolated_like_any_other()
    {
        await RepoFor(TenantId.PlatformValue).InsertAsync(OrderFor(TenantId.PlatformValue, "p1"));
        await RepoFor("shop-a").InsertAsync(OrderFor("shop-a", "a1"));

        (await RepoFor(TenantId.PlatformValue).ListAsync()).Should().ContainSingle();
        (await RepoFor("shop-a").GetByIdAsync("p1")).Should().BeNull();
    }
}
