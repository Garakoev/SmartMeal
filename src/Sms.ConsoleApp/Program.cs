using System.Text;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Sms.Client;
using Sms.Common;
using Sms.ConsoleApp;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
ConsoleTranscript? transcript = null;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    using var configuration = Settings.Load();
    var logDirectory = Path.GetFullPath(configuration["LogDirectory"] ?? "logs", AppContext.BaseDirectory);
    transcript = new ConsoleTranscript(new DailyLog(logDirectory, "test-sms-console-app"));
    var connectionString = configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("В настройках отсутствует ConnectionStrings:Postgres.");
    var options = configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
    if (options.TimeoutSeconds is <= 0 or > 600)
        throw new InvalidOperationException("Api:TimeoutSeconds должен быть от 1 до 600.");
    var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    using var http = new HttpClient { Timeout = timeout };
    using var channel = options.Transport.Equals("Grpc", StringComparison.OrdinalIgnoreCase)
        ? GrpcChannel.ForAddress(options.GrpcEndpoint) : null;
    ISmsClient client = options.Transport.ToUpperInvariant() switch
    {
        "HTTP" => new HttpSmsClient(http, new Uri(options.HttpEndpoint), options.Username, options.Password),
        "GRPC" => new GrpcSmsClient(channel!, timeout),
        _ => throw new InvalidOperationException("Api:Transport должен быть Http или Grpc.")
    };
    Console.WriteLine($"SmartMealService — консольный клиент ({options.Transport})");
    var repository = new PostgresMenuRepository(connectionString,
        configuration["Database:MaintenanceDatabase"] ?? "postgres");
    return await new OrderWorkflow(client, repository, Console.In, Console.Out, transcript.RecordInput)
        .RunAsync(cancellation.Token);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Операция отменена.");
    return 130;
}
catch (Exception ex)
{
    if (transcript is null)
    {
        try { transcript = new ConsoleTranscript(new DailyLog(Path.Combine(AppContext.BaseDirectory, "logs"), "test-sms-console-app")); }
        catch (Exception logError) { Console.Error.WriteLine($"Не удалось открыть файл логов: {logError.Message}"); }
    }
    Console.Error.WriteLine($"Ошибка: {ex.Message}");
    return 1;
}
finally { transcript?.Dispose(); }

internal sealed class ApiOptions
{
    public string Transport { get; set; } = "Http";
    public string HttpEndpoint { get; set; } = "http://localhost:5080/api";
    public string GrpcEndpoint { get; set; } = "http://localhost:5081";
    public string Username { get; set; } = "demo";
    public string Password { get; set; } = "demo";
    public int TimeoutSeconds { get; set; } = 30;
}
