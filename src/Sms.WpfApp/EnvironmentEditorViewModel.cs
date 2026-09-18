using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Sms.Common;

namespace Sms.WpfApp;

public sealed class EditorOptions
{
    public string[] EnvironmentVariables { get; set; } = [];
    public Dictionary<string, string> DefaultValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Comments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class EnvironmentRow(string name, string value, string comment) : Observable
{
    public string Name { get; } = name;
    public string Comment { get; } = comment;
    internal string? ExpectedValue { get; set; } = value;
    private string _value = value;
    public string Value
    {
        get => _value;
        internal set { _value = value; Changed(); }
    }
}

public sealed class EnvironmentEditorViewModel(IEnvironmentStore store, DailyLog log) : Observable
{
    public ObservableCollection<EnvironmentRow> Rows { get; } = [];
    private string _status = "";
    private bool _hasError;
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public bool HasError { get => _hasError; private set { _hasError = value; Changed(); } }

    public void Load(EditorOptions options)
    {
        var names = options.EnvironmentVariables;
        if (names.Length == 0) throw new InvalidOperationException("В appsettings.json не задан массив EnvironmentVariables.");
        if (names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length >= 255 || n.Contains('=') || n.Contains('\0')))
            throw new InvalidOperationException("Недопустимое имя переменной среды в appsettings.json.");
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new InvalidOperationException("Имена переменных среды не должны повторяться.");
        foreach (var name in names)
            if (ValidateValue(options.DefaultValues.GetValueOrDefault(name, "")) is { } error)
                throw new InvalidOperationException($"Значение по умолчанию {name}: {error}");

        Rows.Clear();
        foreach (var name in names)
        {
            var value = store.Read(name);
            if (value is null)
            {
                value = options.DefaultValues.GetValueOrDefault(name, "");
                log.Event("InitializeRequested", new { Name = name, Value = value });
                store.Write(name, value);
                log.Event("Initialized", new { Name = name, Value = value });
            }
            Rows.Add(new(name, value, options.Comments.GetValueOrDefault(name, "")));
        }
        HasError = false;
        Status = "Измените значение и завершите редактирование — оно сохранится автоматически.";
    }

    public bool Save(EnvironmentRow row, string value)
    {
        if (ValidateValue(value) is { } error) return Fail(error);
        try
        {
            var current = store.Read(row.Name);
            if (current == value)
            {
                row.Value = value;
                row.ExpectedValue = value;
                HasError = false;
                Status = $"{row.Name}: изменений нет.";
                return true;
            }
            // Do not silently overwrite changes made by another editor since loading this row.
            if (current != row.ExpectedValue)
            {
                row.Value = current ?? "";
                row.ExpectedValue = current;
                return Fail($"{row.Name} изменена другим приложением. Показано актуальное значение; повторите редактирование.");
            }
            log.Event("ChangeRequested", new { Name = row.Name, OldValue = current, NewValue = value });
            store.Write(row.Name, value);
            row.Value = value;
            row.ExpectedValue = value;
            log.Event("Changed", new { Name = row.Name, OldValue = current, NewValue = value });
            HasError = false;
            Status = $"{row.Name} сохранена · {DateTime.Now:HH:mm:ss}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Fail($"Не удалось завершить сохранение {row.Name}: {ex.Message}");
        }
    }

    private bool Fail(string message) { HasError = true; Status = message; return false; }

    private static string? ValidateValue(string value)
    {
        if (value.Contains('\0')) return "Значение не может содержать нулевой символ.";
        if (value.Length > 32766) return "Превышен предел Windows: допускается до 32 766 символов значения.";
        return null;
    }
}
