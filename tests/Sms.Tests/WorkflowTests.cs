using Sms.Client;
using Sms.ConsoleApp;
using Sms.DemoServer;

namespace Sms.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task RetriesInvalidInputThenSendsValidatedOrder()
    {
        var client = new FakeClient();
        var repo = new FakeRepository();
        using var output = new StringWriter();
        var recorded = new List<string?>();
        var workflow = new OrderWorkflow(client, repo, new StringReader("NO:1\nA1004292:1;A1004293:0,408\n"), output, recorded.Add);
        Assert.Equal(0, await workflow.RunAsync());
        Assert.True(repo.Initialized);
        Assert.Equal(2, repo.Saved!.Count);
        Assert.Equal(2, recorded.Count);
        Assert.Contains("Каша гречневая – A1004292 – 50", output.ToString());
        Assert.Contains("отсутствует в меню", output.ToString());
        Assert.Contains("УСПЕХ", output.ToString());
        Assert.Equal(0.408m, client.Sent!.Items[1].Quantity);
    }

    [Fact]
    public async Task StopsAfterNegativeMenuResponse()
    {
        var client = new FakeClient { MenuError = "Меню недоступно" };
        var repo = new FakeRepository();
        using var output = new StringWriter();
        Assert.Equal(1, await new OrderWorkflow(client, repo, new StringReader(""), output).RunAsync());
        Assert.Null(repo.Saved);
        Assert.Null(client.Sent);
        Assert.Equal("Меню недоступно" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task HandlesEndOfInputWithoutSendingEmptyOrder()
    {
        var client = new FakeClient();
        Assert.Equal(2, await new OrderWorkflow(client, new FakeRepository(), new StringReader(""), new StringWriter()).RunAsync());
        Assert.Null(client.Sent);
    }

    [Fact]
    public async Task DisplaysOrderFailure()
    {
        var client = new FakeClient { OrderError = "Заказ отклонён" };
        using var output = new StringWriter();
        Assert.Equal(1, await new OrderWorkflow(client, new FakeRepository(), new StringReader("A1004292:1"), output).RunAsync());
        Assert.Contains("Заказ отклонён", output.ToString());
        Assert.DoesNotContain("УСПЕХ", output.ToString());
    }

    private sealed class FakeRepository : IMenuRepository
    {
        public bool Initialized { get; private set; }
        public IReadOnlyCollection<Dish>? Saved { get; private set; }
        public Task InitializeAsync(CancellationToken cancellationToken) { Initialized = true; return Task.CompletedTask; }
        public Task SaveAsync(IReadOnlyCollection<Dish> dishes, CancellationToken cancellationToken)
        { Saved = dishes; return Task.CompletedTask; }
    }

    private sealed class FakeClient : ISmsClient
    {
        public string? MenuError { get; init; }
        public string? OrderError { get; init; }
        public Order? Sent { get; private set; }
        public Task<ApiResult<Dish[]>> GetMenuAsync(CancellationToken cancellationToken = default) => Task.FromResult(
            MenuError is null ? ApiResult<Dish[]>.Ok(DemoMenu.Dishes) : ApiResult<Dish[]>.Fail(MenuError));
        public Task<ApiResult<bool>> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
        { Sent = order; return Task.FromResult(OrderError is null ? ApiResult<bool>.Ok(true) : ApiResult<bool>.Fail(OrderError)); }
    }
}
