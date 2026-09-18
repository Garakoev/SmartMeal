using System.Globalization;
using Sms.Client;

namespace Sms.ConsoleApp;

public static class OrderParser
{
    public static ApiResult<OrderItem[]> Parse(string? input, IReadOnlyCollection<Dish> menu)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ApiResult<OrderItem[]>.Fail("Введите хотя бы одну позицию: Артикул:Количество.");

        var byArticle = menu.ToDictionary(d => d.Article, StringComparer.OrdinalIgnoreCase);
        var quantities = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var parts = input.Trim().Split(';').ToList();
        // A single final semicolon is permitted, but empty positions inside the order are not.
        if (parts[^1].Trim().Length == 0) parts.RemoveAt(parts.Count - 1);
        foreach (var part in parts)
        {
            var pair = part.Split(':', StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || pair[0].Length == 0 || pair[1].Length == 0)
                return ApiResult<OrderItem[]>.Fail($"Некорректная позиция «{part}». Формат: Артикул:Количество.");
            if (!byArticle.TryGetValue(pair[0], out var dish))
                return ApiResult<OrderItem[]>.Fail($"Артикул «{pair[0]}» отсутствует в меню.");
            // Both Russian comma and invariant dot are accepted; grouping/exponents are not.
            if (!decimal.TryParse(pair[1].Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                return ApiResult<OrderItem[]>.Fail($"Количество для «{pair[0]}» должно быть числом больше нуля.");
            try { quantities[dish.Id] = checked(quantities.GetValueOrDefault(dish.Id) + quantity); }
            catch (OverflowException)
            { return ApiResult<OrderItem[]>.Fail($"Слишком большое суммарное количество для «{pair[0]}»."); }
        }

        return quantities.Count == 0
            ? ApiResult<OrderItem[]>.Fail("Заказ не должен быть пустым.")
            : ApiResult<OrderItem[]>.Ok(quantities.Select(p => new OrderItem(p.Key, p.Value)).ToArray());
    }
}
