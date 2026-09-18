using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using Sms.WpfApp;

namespace Sms.Tests;

public sealed class WindowsEnvironmentFactAttribute : FactAttribute
{
    public WindowsEnvironmentFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SMS_TEST_ENVIRONMENT") != "1")
            Skip = "Задайте SMS_TEST_ENVIRONMENT=1 для проверки временной переменной в HKCU (с последующим удалением).";
    }
}

public sealed class WindowsEnvironmentIntegrationTests
{
    [WindowsEnvironmentFact]
    public async Task PersistsUnicodeAndEmptyValuesReadableByAnotherProcess()
    {
        var name = "SMS_IT_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var store = new UserEnvironmentStore();
        Assert.Null(store.Read(name));
        try
        {
            var value = "Касса №1\nВторая строка " + new string('Я', 4096);
            store.Write(name, value);
            Assert.Equal(value, new UserEnvironmentStore().Read(name));
            // This process explicitly reads the persistent user scope rather than its inherited snapshot.
            var command = $"[Console]::Write([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([Environment]::GetEnvironmentVariable('{name}', 'User'))))";
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
            using var process = Process.Start(start)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(value, Encoding.UTF8.GetString(Convert.FromBase64String(output.Trim())));
            store.Write(name, "");
            Assert.Equal("", store.Read(name));
            using var key = Registry.CurrentUser.OpenSubKey("Environment");
            Assert.Contains(name, key!.GetValueNames());
        }
        finally
        {
            // The unique test variable did not exist before this test; no pre-existing values are modified.
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
        }
    }
}
