using System.Reflection;

namespace IGBZ.Infrastructure.Mongo;

/// <summary>خوانندهٔ کش‌شدهٔ پراپرتی <c>Id</c> برای عملیات Replace/Delete.</summary>
internal static class IdAccessor<T>
{
    private static readonly PropertyInfo IdProperty =
        typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' does not expose a public Id property.");

    public static string GetId(T entity)
    {
        var value = IdProperty.GetValue(entity) as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' has an empty Id.");
        }

        return value;
    }
}
