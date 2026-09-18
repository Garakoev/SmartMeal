using Sms.Common;

namespace Sms.Tests;

[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class ConsoleCollection;

[Collection("Console")]
public sealed class LoggingTests
{
    [Fact]
    public void CapturesStdoutStderrInputAndRollsAtMidnight()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sms-log-test-" + Guid.NewGuid().ToString("N"));
        var now = new DateTime(2026, 9, 18, 23, 59, 59);
        try
        {
            var log = new DailyLog(directory, "test-sms-console-app", () => now);
            var original = Console.Out;
            using (var transcript = new ConsoleTranscript(log))
            {
                Console.WriteLine("Меню");
                transcript.RecordInput("A1004292:1");
                Console.Error.WriteLine("Ошибка");
                now = now.AddSeconds(2);
                Console.WriteLine("Новый день");
            }
            Assert.Same(original, Console.Out);
            var first = File.ReadAllText(Path.Combine(directory, "test-sms-console-app-20260918.log"));
            Assert.Contains("Меню", first);
            Assert.Contains("A1004292:1", first);
            Assert.Contains("Ошибка", first);
            Assert.Equal("Новый день" + Environment.NewLine,
                File.ReadAllText(Path.Combine(directory, "test-sms-console-app-20260919.log")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
