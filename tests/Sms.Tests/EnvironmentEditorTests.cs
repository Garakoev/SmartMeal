using Sms.Common;
using Sms.WpfApp;

namespace Sms.Tests;

public sealed class EnvironmentEditorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sms-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeEnvironmentStore _store = new();
    private EnvironmentEditorViewModel Create() => new(_store, new DailyLog(_directory, "test-sms-wpf-app"));
    private static EditorOptions Options => new()
    {
        EnvironmentVariables = ["SMS_TEST_A", "SMS_TEST_B"],
        DefaultValues = new() { ["SMS_TEST_A"] = "default" }
    };

    [Fact]
    public void InitializesOnlyMissingVariablesIncludingEmptyDefaults()
    {
        _store.Values["SMS_TEST_A"] = "existing";
        var vm = Create();
        vm.Load(Options);
        Assert.Equal("existing", vm.Rows[0].Value);
        Assert.Equal("", _store.Values["SMS_TEST_B"]);
        Assert.Equal(1, _store.Writes);
        Create().Load(Options);
        Assert.Equal(1, _store.Writes);
    }

    [Fact]
    public void SavesLongMultilineAndEmptyValuesAndAuditsEachChange()
    {
        var vm = Create();
        vm.Load(Options);
        var value = new string('Я', 10000) + "\nвторая строка";
        Assert.True(vm.Save(vm.Rows[0], value));
        Assert.Equal(value, _store.Values["SMS_TEST_A"]);
        Assert.True(vm.Save(vm.Rows[0], ""));
        Assert.Equal("", _store.Values["SMS_TEST_A"]);
        var lines = File.ReadAllLines(Assert.Single(Directory.GetFiles(_directory)));
        Assert.Equal(2, lines.Count(l => l.Contains("\"Action\":\"Changed\"")));
    }

    [Fact]
    public void NoOpDoesNotWriteOrLogAChange()
    {
        var vm = Create();
        vm.Load(Options);
        var writes = _store.Writes;
        Assert.True(vm.Save(vm.Rows[0], vm.Rows[0].Value));
        Assert.Equal(writes, _store.Writes);
    }

    [Fact]
    public void DoesNotOverwriteConcurrentExternalChanges()
    {
        var vm = Create();
        vm.Load(Options);
        _store.Values["SMS_TEST_A"] = "external";
        Assert.False(vm.Save(vm.Rows[0], "mine"));
        Assert.Equal("external", vm.Rows[0].Value);
        Assert.Equal("external", _store.Values["SMS_TEST_A"]);
        Assert.True(vm.HasError);
    }

    [Fact]
    public void RejectsSystemLimitAndNulWithoutChangingStorage()
    {
        var vm = Create();
        vm.Load(Options);
        Assert.False(vm.Save(vm.Rows[0], new string('a', 32767)));
        Assert.False(vm.Save(vm.Rows[0], "a\0b"));
        Assert.Equal("default", _store.Values["SMS_TEST_A"]);
    }

    [Fact]
    public void CanRecreateVariableAfterNotifyingAboutExternalDeletion()
    {
        var vm = Create();
        vm.Load(Options);
        _store.Values.Remove("SMS_TEST_A");
        Assert.False(vm.Save(vm.Rows[0], "replacement"));
        Assert.True(vm.Save(vm.Rows[0], "replacement"));
        Assert.Equal("replacement", _store.Values["SMS_TEST_A"]);
    }

    [Fact]
    public void DuplicateConfigurationIsRejectedBeforeWrites()
    {
        var vm = Create();
        Assert.Throws<InvalidOperationException>(() => vm.Load(new EditorOptions { EnvironmentVariables = ["NAME", "name"] }));
        Assert.Equal(0, _store.Writes);
    }

    [Fact]
    public void ReportsStorageFailureWithoutClaimingSuccess()
    {
        var vm = Create();
        vm.Load(Options);
        _store.FailWrites = true;
        Assert.False(vm.Save(vm.Rows[0], "changed"));
        Assert.Equal("default", vm.Rows[0].Value);
        Assert.True(vm.HasError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

internal sealed class FakeEnvironmentStore : IEnvironmentStore
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int Writes { get; private set; }
    public bool FailWrites { get; set; }
    public string? Read(string name) => Values.GetValueOrDefault(name);
    public void Write(string name, string value)
    {
        if (FailWrites) throw new UnauthorizedAccessException("Test access denied");
        Writes++;
        Values[name] = value;
    }
}
