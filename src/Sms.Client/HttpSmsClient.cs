using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sms.Client;

/// <summary>The caller owns the injected HttpClient and configures its timeout.</summary>
public sealed class HttpSmsClient : ISmsClient
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly AuthenticationHeaderValue _authorization;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public HttpSmsClient(HttpClient http, Uri endpoint, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
            throw new ArgumentException("Требуется абсолютный HTTP(S) адрес.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(username) || username.Contains(':'))
            throw new ArgumentException("Логин не должен быть пустым или содержать двоеточие.", nameof(username));
        _http = http;
        _endpoint = endpoint;
        _authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
    }

    public async Task<ApiResult<Dish[]>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync<MenuResponse>(
            new { Command = "GetMenu", CommandParameters = new { WithPrice = true } }, cancellationToken);
        if (!response.Success) return ApiResult<Dish[]>.Fail(response.ErrorMessage);
        var body = response.Value!;
        if (!body.Success) return ApiResult<Dish[]>.Fail(body.ErrorMessage);
        if (body.Data?.MenuItems is not { } items)
            return ApiResult<Dish[]>.Fail("В ответе сервера отсутствует Data.MenuItems.");
        var error = Validation.Menu(items);
        return error is null ? ApiResult<Dish[]>.Ok(items) : ApiResult<Dish[]>.Fail(error);
    }

    public async Task<ApiResult<bool>> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (Validation.Order(order) is { } error) return ApiResult<bool>.Fail(error);
        var response = await ExecuteAsync<CommandResponse>(new
        {
            Command = "SendOrder",
            CommandParameters = new
            {
                OrderId = order.Id.ToString(),
                MenuItems = order.Items.Select(i => new
                {
                    i.Id,
                    // HTTP contract uses a JSON string with an invariant decimal separator.
                    Quantity = i.Quantity.ToString(CultureInfo.InvariantCulture)
                })
            }
        }, cancellationToken);
        if (!response.Success) return ApiResult<bool>.Fail(response.ErrorMessage);
        return response.Value!.Success ? ApiResult<bool>.Ok(true)
            : ApiResult<bool>.Fail(response.Value.ErrorMessage);
    }

    private async Task<ApiResult<T>> ExecuteAsync<T>(object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Authorization = _authorization;
            request.Content = JsonContent.Create(payload, options: Json);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiResult<T>.Fail($"HTTP-ошибка: {(int)response.StatusCode} ({response.ReasonPhrase}).");
            var body = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            return body is null ? ApiResult<T>.Fail("Сервер вернул пустой ответ.") : ApiResult<T>.Ok(body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Истекло время ожидания HTTP-ответа.");
        }
        catch (HttpRequestException ex) { return ApiResult<T>.Fail($"Ошибка HTTP-соединения: {ex.Message}"); }
        catch (JsonException) { return ApiResult<T>.Fail("Сервер вернул некорректный JSON или неверную структуру ответа."); }
    }

    private class CommandResponse
    {
        [JsonRequired] public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
    }

    private sealed class MenuResponse : CommandResponse
    {
        public MenuData? Data { get; init; }
    }

    private sealed class MenuData
    {
        public Dish[]? MenuItems { get; init; }
    }
}
