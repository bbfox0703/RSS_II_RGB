namespace RSS_II_RGB.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Sink for engine/log output. The platform-agnostic seam for logging
/// (CLAUDE.md rule 4/6) — implementations (rotating file, console) live in the
/// platform/app projects.
/// </summary>
public interface ILogSink
{
    void Log(LogLevel level, string message, Exception? exception = null);
}
