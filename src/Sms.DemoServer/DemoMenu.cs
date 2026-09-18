using System.Collections.Concurrent;
using Sms.Client;

namespace Sms.DemoServer;

public sealed class DemoMenu(IConfiguration configuration)
{
    public static readonly Dish[] Dishes =
    [
        new("5979224", "A1004292", "Каша гречневая", 50m, false, @"ПРОИЗВОДСТВО\Гарниры", ["57890975627974236429"]),
        new("9084246", "A1004293", "Конфеты Коровка", 300m, true, @"ДЕСЕРТЫ\Развес", [])
    ];

    public ConcurrentQueue<Order> ReceivedOrders { get; } = new();
    public string? MenuError => configuration["Demo:MenuError"];

    public string? Accept(Order order)
    {
        if (!string.IsNullOrWhiteSpace(configuration["Demo:OrderError"])) return configuration["Demo:OrderError"];
        if (order.Id == Guid.Empty || order.Items.Count == 0) return "Пустой заказ или неверный OrderId.";
        if (order.Items.Any(i => i.Quantity <= 0 || !Dishes.Any(d => d.Id == i.Id)))
            return "Неизвестное блюдо или неположительное количество.";
        ReceivedOrders.Enqueue(order);
        return null;
    }
}
