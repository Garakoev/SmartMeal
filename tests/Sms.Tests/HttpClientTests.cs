using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Sms.Client;
using Sms.DemoServer;

namespace Sms.Tests;

public sealed class HttpClientTests
{
    [Fact]
    public async Task SendsBasicAuthAndExactMenuContract()
    {
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost/api", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal("user:pass", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)));
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("GetMenu", json.RootElement.GetProperty("Command").GetString());
            Assert.True(json.RootElement.GetProperty("CommandParameters").GetProperty("WithPrice").GetBoolean());
            return Json(new { Success = true, Data = new { MenuItems = DemoMenu.Dishes } });
        }));
        var result = await Client(http).GetMenuAsync();
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(300m, result.Value![1].Price);
        Assert.Equal("57890975627974236429", result.Value[0].Barcodes[0]);
    }

    [Fact]
    public async Task SendsStringQuantityWithDotRegardlessOfCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            var id = Guid.NewGuid();
            using var http = new HttpClient(new Handler(async (request, ct) =>
            {
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var root = json.RootElement;
                Assert.Equal("SendOrder", root.GetProperty("Command").GetString());
                var parameters = root.GetProperty("CommandParameters");
                Assert.Equal(id.ToString(), parameters.GetProperty("OrderId").GetString());
                var item = parameters.GetProperty("MenuItems")[0];
                Assert.Equal("9084246", item.GetProperty("Id").GetString());
                Assert.Equal(JsonValueKind.String, item.GetProperty("Quantity").ValueKind);
                Assert.Equal("0.408", item.GetProperty("Quantity").GetString());
                return Json(new { Success = true });
            }));
            Assert.True((await Client(http).SendOrderAsync(new Order(id, [new("9084246", 0.408m)]))).Success);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public async Task Http200WithSuccessFalseIsAnError()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { Success = false, ErrorMessage = "Нет меню" }))));
        var result = await Client(http).GetMenuAsync();
        Assert.False(result.Success);
        Assert.Equal("Нет меню", result.ErrorMessage);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Success\":true}")]
    [InlineData("{\"Success\":true,\"Data\":{\"MenuItems\":[null]}}")]
    [InlineData("{\"Success\":true,\"Data\":{\"MenuItems\":[{\"Id\":\"1\",\"Article\":\"A\",\"Name\":\"Dish\",\"IsWeighted\":false,\"FullPath\":\"\",\"Barcodes\":[]}]}}")]
    public async Task RejectsMalformedMenuResponses(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") })));
        Assert.False((await Client(http).GetMenuAsync()).Success);
    }

    [Fact]
    public async Task HandlesNonSuccessHttpStatus()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        Assert.Contains("401", (await Client(http).GetMenuAsync()).ErrorMessage);
    }

    [Fact]
    public async Task PropagatesCallerCancellation()
    {
        using var http = new HttpClient(new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(new { }); }));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).GetMenuAsync(cts.Token));
    }

    [Fact]
    public async Task ReportsTimeout()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        Assert.Contains("время ожидания", (await Client(http).GetMenuAsync()).ErrorMessage);
    }

    [Fact]
    public async Task RejectsAmbiguousMenuArticles()
    {
        var dishes = new[] { DemoMenu.Dishes[0], DemoMenu.Dishes[1] with { Article = DemoMenu.Dishes[0].Article.ToLowerInvariant() } };
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { Success = true, Data = new { MenuItems = dishes } }))));
        Assert.Contains("повторяющиеся", (await Client(http).GetMenuAsync()).ErrorMessage);
    }

    [Fact]
    public async Task DoesNotSendInvalidOrder()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Must not send")));
        Assert.False((await Client(http).SendOrderAsync(new Order(Guid.NewGuid(), [new("1", 0)]))).Success);
    }

    private static HttpSmsClient Client(HttpClient http) => new(http, new Uri("http://localhost/api"), "user", "pass");
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
