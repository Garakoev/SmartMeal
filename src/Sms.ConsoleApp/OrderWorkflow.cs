using System.Globalization;
using Sms.Client;

namespace Sms.ConsoleApp;

public sealed class OrderWorkflow(ISmsClient client, IMenuRepository repository,
    TextReader input, TextWriter output, Action<string?>? recordInput = null)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await repository.InitializeAsync(cancellationToken);
        var response = await client.GetMenuAsync(cancellationToken);
        if (!response.Success)
        {
            await output.WriteLineAsync(response.ErrorMessage);
            return 1;
        }
        var menu = response.Value!;
        await repository.SaveAsync(menu, cancellationToken);
        foreach (var dish in menu)
            await output.WriteLineAsync($"{dish.Name} – {dish.Article} – {dish.Price.ToString("0.############################", CultureInfo.InvariantCulture)}");
        if (menu.Length == 0)
        {
            await output.WriteLineAsync("Меню пусто. Составить заказ невозможно.");
            return 1;
        }

        var order = new Order(Guid.NewGuid(), []);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await output.WriteLineAsync("Введите заказ: Артикул:Количество;Артикул:Количество (Ctrl+C — выход)");
            var line = await input.ReadLineAsync(cancellationToken);
            recordInput?.Invoke(line);
            if (line is null)
            {
                await output.WriteLineAsync("Ввод завершён. Заказ не отправлен.");
                return 2;
            }
            var parsed = OrderParser.Parse(line, menu);
            if (!parsed.Success)
            {
                await output.WriteLineAsync(parsed.ErrorMessage);
                continue;
            }
            order = order with { Items = parsed.Value! };
            break;
        }

        var sent = await client.SendOrderAsync(order, cancellationToken);
        await output.WriteLineAsync(sent.Success ? "УСПЕХ" : sent.ErrorMessage);
        return sent.Success ? 0 : 1;
    }
}
