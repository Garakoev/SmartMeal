using System.Text;
using System.Text.Json;

namespace Sms.Common;

/// <summary>Appends UTF-8 text; resolves the file date on every write (including after midnight).</summary>
public sealed class DailyLog
{
    private readonly string _directory;
    private readonly string _prefix;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();

    public DailyLog(string directory, string prefix, Func<DateTime>? now = null)
    {
        _directory = Path.GetFullPath(directory);
        _prefix = prefix;
        _now = now ?? (() => DateTime.Now);
        Directory.CreateDirectory(_directory);
    }

    public void Write(string text)
    {
        lock (_gate)
            File.AppendAllText(Path.Combine(_directory, $"{_prefix}-{_now():yyyyMMdd}.log"), text, new UTF8Encoding(false));
    }

    public void Event(string action, object details) => Write(
        JsonSerializer.Serialize(new { Timestamp = _now().ToString("O"), Action = action, Details = details })
        + Environment.NewLine);
}
