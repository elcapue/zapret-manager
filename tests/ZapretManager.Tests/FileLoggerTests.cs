using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class FileLoggerTests
{
    [Fact]
    public void Info_WritesMessageToLogFile()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-log-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        var logger = new FileLogger(layout);

        logger.Info("hello runtime");

        var logPath = Path.Combine(layout.LogsDirectory, "zapret-manager.log");
        Assert.True(File.Exists(logPath));
        Assert.Contains("hello runtime", File.ReadAllText(logPath));
    }
}
