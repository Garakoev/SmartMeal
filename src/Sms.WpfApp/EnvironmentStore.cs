using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Sms.WpfApp;

public interface IEnvironmentStore
{
    string? Read(string name);
    void Write(string name, string value);
}

/// <summary>HKCU values persist across reboots and are inherited by newly started user applications.</summary>
public sealed class UserEnvironmentStore : IEnvironmentStore
{
    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment");
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void Write(string name, string value)
    {
        // .NET 8 Environment.SetEnvironmentVariable deletes an empty value. Registry REG_SZ
        // lets the editor preserve an intentional empty string as an existing variable.
        using (var key = Registry.CurrentUser.CreateSubKey("Environment", writable: true))
            key.SetValue(name, value, RegistryValueKind.String);
        // Notify Explorer without waiting indefinitely for an unresponsive window.
        _ = SendMessageTimeout(new IntPtr(0xffff), 0x001a, IntPtr.Zero, "Environment",
            0x0002, 2000, out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        string lParam, uint flags, uint timeout, out IntPtr result);
}
