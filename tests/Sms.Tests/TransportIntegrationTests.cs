using System.Net;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Client;
using Sms.DemoServer;

namespace Sms.Tests;

public sealed class TransportIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private string _httpAddress = "";
    private string _grpcAddress = "";

    public async Task InitializeAsync()
    {
        _app = DemoApplication.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Username"] = "demo", ["Demo:Password"] = "demo"
            });
            builder.WebHost.ConfigureKestrel(o =>
            {
                o.Listen(IPAddress.Loopback, 0, e => e.Protocols = HttpProtocols.Http1);
                o.Listen(IPAddress.Loopback, 0, e => e.Protocols = HttpProtocols.Http2);
            });
        });
        await _app.StartAsync();
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.ToArray();
        _httpAddress = addresses[0];
        _grpcAddress = addresses[1];
    }

    public async Task DisposeAsync() { await _app.StopAsync(); await _app.DisposeAsync(); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealServerReturnsMenuAndReceivesOrder(bool grpc)
    {
        using var http = new HttpClient();
        using var channel = GrpcChannel.ForAddress(_grpcAddress);
        ISmsClient client = grpc ? new GrpcSmsClient(channel)
            : new HttpSmsClient(http, new Uri(_httpAddress + "/api"), "demo", "demo");
        var menu = await client.GetMenuAsync();
        Assert.True(menu.Success, menu.ErrorMessage);
        Assert.Equal(2, menu.Value!.Length);
        Assert.Equal("Конфеты Коровка", menu.Value[1].Name);
        var order = new Order(Guid.NewGuid(), [new("9084246", 0.408m)]);
        var result = await client.SendOrderAsync(order);
        Assert.True(result.Success, result.ErrorMessage);
        var received = Assert.Single(_app.Services.GetRequiredService<DemoMenu>().ReceivedOrders);
        Assert.Equal(order.Id, received.Id);
        Assert.Equal(0.408m, Assert.Single(received.Items).Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothTransportsPreserveApplicationErrors(bool grpc)
    {
        _app.Configuration["Demo:MenuError"] = "Меню временно недоступно";
        _app.Configuration["Demo:OrderError"] = "Заказ отклонён";
        using var http = new HttpClient();
        using var channel = GrpcChannel.ForAddress(_grpcAddress);
        ISmsClient client = grpc ? new GrpcSmsClient(channel)
            : new HttpSmsClient(http, new Uri(_httpAddress + "/api"), "demo", "demo");
        var menu = await client.GetMenuAsync();
        Assert.False(menu.Success);
        Assert.Equal("Меню временно недоступно", menu.ErrorMessage);
        var order = await client.SendOrderAsync(new Order(Guid.NewGuid(), [new("5979224", 1)]));
        Assert.False(order.Success);
        Assert.Equal("Заказ отклонён", order.ErrorMessage);
    }

    [Fact]
    public async Task GrpcPropagatesCancellation()
    {
        using var channel = GrpcChannel.ForAddress(_grpcAddress);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GrpcSmsClient(channel).GetMenuAsync(cancellation.Token));
    }
}
