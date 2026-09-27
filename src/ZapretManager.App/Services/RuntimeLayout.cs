namespace ZapretManager.App.Services;

public sealed class RuntimeLayout
{
    private RuntimeLayout(string baseDirectory)
    {
        BaseDirectory = Path.GetFullPath(baseDirectory);
        RuntimeDirectory = Path.Combine(BaseDirectory, "runtime");
        TempDirectory = Path.Combine(BaseDirectory, "temp");
        BackupsDirectory = Path.Combine(BaseDirectory, "backups");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
    }

    public string BaseDirectory { get; }

    public string RuntimeDirectory { get; }

    public string TempDirectory { get; }

    public string BackupsDirectory { get; }

    public string LogsDirectory { get; }

    public static RuntimeLayout ForDirectory(string baseDirectory)
    {
        return new RuntimeLayout(baseDirectory);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
