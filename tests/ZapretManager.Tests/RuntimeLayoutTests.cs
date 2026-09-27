using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class RuntimeLayoutTests
{
    [Fact]
    public void ForDirectory_UsesExpectedDirectoriesUnderBaseDirectory()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var layout = RuntimeLayout.ForDirectory(baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "runtime"), layout.RuntimeDirectory);
        Assert.Equal(Path.Combine(baseDirectory, "temp"), layout.TempDirectory);
        Assert.Equal(Path.Combine(baseDirectory, "backups"), layout.BackupsDirectory);
    }

    [Fact]
    public void EnsureDirectories_CreatesAllManagedDirectories()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var layout = RuntimeLayout.ForDirectory(baseDirectory);

        layout.EnsureDirectories();

        Assert.True(Directory.Exists(layout.RuntimeDirectory));
        Assert.True(Directory.Exists(layout.TempDirectory));
        Assert.True(Directory.Exists(layout.BackupsDirectory));
        Assert.True(Directory.Exists(layout.LogsDirectory));
    }
}
