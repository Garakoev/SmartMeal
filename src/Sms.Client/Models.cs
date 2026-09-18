using System.Text.Json.Serialization;

namespace Sms.Client;

public sealed record Dish(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Article,
    [property: JsonRequired] string Name,
    [property: JsonRequired] decimal Price,
    [property: JsonRequired] bool IsWeighted,
    [property: JsonRequired] string FullPath,
    [property: JsonRequired] string[] Barcodes);

public sealed record OrderItem(string Id, decimal Quantity);

public sealed record Order(Guid Id, IReadOnlyList<OrderItem> Items);

public sealed class ApiResult<T>
{
    private ApiResult(bool success, T? value, string errorMessage)
        => (Success, Value, ErrorMessage) = (success, value, errorMessage);

    public bool Success { get; }
    public T? Value { get; }
    public string ErrorMessage { get; }

    public static ApiResult<T> Ok(T value) => new(true, value, string.Empty);
    public static ApiResult<T> Fail(string? message) => new(false, default,
        string.IsNullOrWhiteSpace(message) ? "Сервер сообщил об ошибке без описания." : message);
}

public interface ISmsClient
{
    Task<ApiResult<Dish[]>> GetMenuAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> SendOrderAsync(Order order, CancellationToken cancellationToken = default);
}

internal static class Validation
{
    public static string? Menu(Dish[] dishes)
    {
        if (dishes.Any(d => d is null || string.IsNullOrWhiteSpace(d.Id)
            || string.IsNullOrWhiteSpace(d.Article) || string.IsNullOrWhiteSpace(d.Name)
            || d.Price < 0 || d.FullPath is null || d.Barcodes is null))
            return "Сервер вернул некорректные данные блюда.";
        if (dishes.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != dishes.Length
            || dishes.Select(d => d.Article).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dishes.Length)
            return "Меню содержит повторяющиеся Id или артикулы.";
        return null;
    }

    public static string? Order(Order order)
    {
        if (order.Id == Guid.Empty || order.Items is null || order.Items.Count == 0)
            return "Заказ должен иметь идентификатор и хотя бы одну позицию.";
        return order.Items.Any(i => i is null || string.IsNullOrWhiteSpace(i.Id) || i.Quantity <= 0)
            ? "Все позиции заказа должны иметь Id и положительное количество." : null;
    }
}
