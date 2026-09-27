namespace ZapretManager.App.Services;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class FileLogger : IAppLogger
{
    private readonly string _logPath;

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
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        File.AppendAllText(_logPath, line + Environment.NewLine);
    }
}
