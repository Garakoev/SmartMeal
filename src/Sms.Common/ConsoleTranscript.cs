using System.Text;

namespace Sms.Common;

/// <summary>Duplicates stdout/stderr and explicitly records input, which Console.SetOut cannot intercept.</summary>
public sealed class ConsoleTranscript : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly DailyLog _log;

    public ConsoleTranscript(DailyLog log)
    {
        _log = log;
        Console.SetOut(TextWriter.Synchronized(new TeeWriter(_originalOut, log)));
        Console.SetError(TextWriter.Synchronized(new TeeWriter(_originalError, log)));
    }

    public void RecordInput(string? input) => _log.Write((input ?? "<конец ввода>") + Environment.NewLine);

    public void Dispose()
    {
        Console.Out.Flush();
        Console.Error.Flush();
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }

    private sealed class TeeWriter(TextWriter original, DailyLog log) : TextWriter
    {
        public override Encoding Encoding => original.Encoding;
        public override void Write(char value) => Write(value.ToString());
        public override void Write(string? value)
        {
            if (value is null) return;
            log.Write(value);
            original.Write(value);
        }
        public override void WriteLine(string? value) => Write(value + NewLine);
        public override void Flush() => original.Flush();
    }
}
