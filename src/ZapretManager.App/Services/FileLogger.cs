namespace ZapretManager.App.Services;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class FileLogger : IAppLogger
{
    private readonly string _logPath;
    private readonly object _writeGate = new();

    public FileLogger(RuntimeLayout layout)
    {
        _logPath = Path.Combine(layout.LogsDirectory, "zapret-manager.log");
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        // Логгер вызывается в том числе из catch-блоков и фоновых задач:
        // он не должен сам стать источником исключений или гонок записи.
        try
        {
            lock (_writeGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Некуда сообщить о сбое лога — просто не падаем.
        }
    }
}
