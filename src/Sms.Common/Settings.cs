using Microsoft.Extensions.Configuration;

namespace Sms.Common;

public static class Settings
{
    public static ConfigurationManager Load(string? directory = null)
    {
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(directory ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables("SMS_");
        return configuration;
    }
}
