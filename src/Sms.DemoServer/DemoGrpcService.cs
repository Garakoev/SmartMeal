using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Sms.Test;

namespace Sms.DemoServer;

public sealed class DemoGrpcService(DemoMenu menu) : SmsTestService.SmsTestServiceBase
{
    public override Task<GetMenuResponse> GetMenu(BoolValue request, ServerCallContext context)
    {
        if (!string.IsNullOrWhiteSpace(menu.MenuError))
            return Task.FromResult(new GetMenuResponse { ErrorMessage = menu.MenuError });
        var response = new GetMenuResponse { Success = true };
        response.MenuItems.AddRange(DemoMenu.Dishes.Select(d =>
        {
            var item = new MenuItem { Id = d.Id, Article = d.Article, Name = d.Name,
                Price = request.Value ? (double)d.Price : 0, IsWeighted = d.IsWeighted, FullPath = d.FullPath };
            item.Barcodes.AddRange(d.Barcodes);
            return item;
        }));
        return Task.FromResult(response);
    }

    public override Task<SendOrderResponse> SendOrder(Order request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            return Task.FromResult(new SendOrderResponse { ErrorMessage = "Некорректный OrderId." });
        try
        {
            var error = menu.Accept(new Client.Order(id,
                request.OrderItems.Select(i => new Client.OrderItem(i.Id, checked((decimal)i.Quantity))).ToArray()));
            return Task.FromResult(new SendOrderResponse { Success = error is null, ErrorMessage = error ?? "" });
        }
        catch (OverflowException)
        { return Task.FromResult(new SendOrderResponse { ErrorMessage = "Некорректное количество." }); }
    }
}
