using System.Text;
using RSS_II_RGB.Core.Logging;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Writes logs to %LOCALAPPDATA%\RSS_II_RGB\Logs\ with 4-file rotation, 8 MB max
/// each (CLAUDE.md rules 6/7). Thread-safe; writes are low-volume (engine start/
/// stop and errors, not the render hot path), so a lock is sufficient.
/// </summary>
public sealed class RotatingFileLogSink : ILogSink, IDisposable
{
    private const long MaxBytes = 8 * 1024 * 1024;
    private const int FileCount = 4;
    private const string BaseName = "rss.log";

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _activePath;
    private StreamWriter? _writer;

    public RotatingFileLogSink()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _directory = Path.Combine(root, "RSS_II_RGB", "Logs");
        Directory.CreateDirectory(_directory);
        _activePath = Path.Combine(_directory, BaseName);
        OpenWriter();
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        string line = exception is null
            ? $"{Timestamp()} [{level}] {message}"
            : $"{Timestamp()} [{level}] {message} :: {exception}";

        lock (_gate)
        {
            if (_writer is null)
            {
                return;
            }

            RotateIfNeeded();
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

    private void OpenWriter()
    {
        _writer = new StreamWriter(_activePath, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    private void RotateIfNeeded()
    {
        if (_writer is null || _writer.BaseStream.Length < MaxBytes)
        {
            return;
        }

        _writer.Dispose();
        _writer = null;

        // Drop the oldest, then shift rss.(n-1).log -> rss.n.log, rss.log -> rss.1.log.
        string oldest = IndexedPath(FileCount - 1);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }
        for (int i = FileCount - 2; i >= 1; i--)
        {
            MoveIfExists(IndexedPath(i), IndexedPath(i + 1));
        }
        MoveIfExists(_activePath, IndexedPath(1));

        OpenWriter();
    }

    private string IndexedPath(int index) => Path.Combine(_directory, $"rss.{index}.log");

    private static void MoveIfExists(string from, string to)
    {
        if (File.Exists(from))
        {
            File.Move(from, to, overwrite: true);
        }
    }
}
