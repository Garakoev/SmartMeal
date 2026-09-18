using System.Globalization;
using System.Text;
using System.Text.Json;
using Sms.Client;

namespace Sms.DemoServer;

public static class DemoApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<DemoMenu>();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null);
        var app = builder.Build();
        app.MapGrpcService<DemoGrpcService>();
        app.MapGet("/health", () => Results.Ok(new { Status = "ok" }));
        app.MapPost("/api", HandleHttp);
        return app;
    }

    private static async Task<IResult> HandleHttp(HttpRequest request, DemoMenu menu, IConfiguration config)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{config["Demo:Username"] ?? "demo"}:{config["Demo:Password"] ?? "demo"}"));
        if (request.Headers.Authorization != $"Basic {credentials}")
            return Results.Json(new { Command = "", Success = false, ErrorMessage = "Ошибка Basic-аутентификации." }, statusCode: 401);

        string command = "";
        try
        {
            using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            command = body.RootElement.GetProperty("Command").GetString() ?? "";
            var parameters = body.RootElement.GetProperty("CommandParameters");
            if (command == "GetMenu")
            {
                if (!string.IsNullOrWhiteSpace(menu.MenuError)) return Error(command, menu.MenuError);
                var withPrice = parameters.GetProperty("WithPrice").GetBoolean();
                return Results.Ok(new { Command = command, Success = true, ErrorMessage = "",
                    Data = new { MenuItems = DemoMenu.Dishes.Select(d => withPrice ? d : d with { Price = 0 }) } });
            }
            if (command == "SendOrder")
            {
                if (!Guid.TryParse(parameters.GetProperty("OrderId").GetString(), out var id))
                    return Error(command, "Некорректный OrderId.");
                var items = new List<OrderItem>();
                foreach (var item in parameters.GetProperty("MenuItems").EnumerateArray())
                {
                    if (!decimal.TryParse(item.GetProperty("Quantity").GetString(), NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out var quantity))
                        return Error(command, "Quantity должна быть строкой с положительным числом.");
                    items.Add(new OrderItem(item.GetProperty("Id").GetString() ?? "", quantity));
                }
                var error = menu.Accept(new Order(id, items));
                return error is null ? Results.Ok(new { Command = command, Success = true, ErrorMessage = "" }) : Error(command, error);
            }
            return Error(command, "Неизвестная команда.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return Error(command, "Некорректная структура запроса."); }
    }

    private static IResult Error(string command, string message) => Results.Ok(new
        { Command = command, Success = false, ErrorMessage = message });
}
