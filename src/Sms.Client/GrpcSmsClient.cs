using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Sms.Test;

namespace Sms.Client;

/// <summary>The caller owns the channel. No authentication is used by this transport.</summary>
public sealed class GrpcSmsClient : ISmsClient
{
    private readonly SmsTestService.SmsTestServiceClient _client;
    private readonly TimeSpan _timeout;

    public GrpcSmsClient(GrpcChannel channel, TimeSpan? timeout = null)
    {
        _client = new(channel);
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (_timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<ApiResult<Dish[]>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetMenuAsync(new BoolValue { Value = true },
                deadline: DateTime.UtcNow + _timeout, cancellationToken: cancellationToken);
            if (!response.Success) return ApiResult<Dish[]>.Fail(response.ErrorMessage);
            var items = response.MenuItems.Select(i => new Dish(i.Id, i.Article, i.Name,
                checked((decimal)i.Price), i.IsWeighted, i.FullPath, i.Barcodes.ToArray())).ToArray();
            var error = Validation.Menu(items);
            return error is null ? ApiResult<Dish[]>.Ok(items) : ApiResult<Dish[]>.Fail(error);
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (RpcException ex) { return ApiResult<Dish[]>.Fail($"Ошибка gRPC ({ex.StatusCode}): {ex.Status.Detail}"); }
        catch (OverflowException) { return ApiResult<Dish[]>.Fail("Сервер вернул цену вне допустимого диапазона."); }
    }

    public async Task<ApiResult<bool>> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (Validation.Order(order) is { } error) return ApiResult<bool>.Fail(error);
        try
        {
            var request = new Test.Order { Id = order.Id.ToString() };
            request.OrderItems.AddRange(order.Items.Select(i => new Test.OrderItem
                { Id = i.Id, Quantity = (double)i.Quantity }));
            var response = await _client.SendOrderAsync(request,
                deadline: DateTime.UtcNow + _timeout, cancellationToken: cancellationToken);
            return response.Success ? ApiResult<bool>.Ok(true) : ApiResult<bool>.Fail(response.ErrorMessage);
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (RpcException ex) { return ApiResult<bool>.Fail($"Ошибка gRPC ({ex.StatusCode}): {ex.Status.Detail}"); }
    }
}
