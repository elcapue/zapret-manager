using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class RuntimeValidatorTests
{
    [Fact]
    public void Validate_WhenRuntimeIsEmpty_ReportsRequiredZapretFilesAsMissing()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-runtime-test-");
        var layout = RuntimeLayout.ForDirectory(tempDir.FullName);
        layout.EnsureDirectories();

        var result = RuntimeValidator.Validate(layout.RuntimeDirectory);

        Assert.False(result.IsComplete);
        Assert.Contains("bin/winws.exe", result.MissingItems);
        Assert.Contains("bin/WinDivert64.sys", result.MissingItems);
        Assert.Contains("bin/WinDivert.dll", result.MissingItems);
        Assert.Contains("service.bat", result.MissingItems);
        Assert.Contains("general*.bat", result.MissingItems);
    }

    [Fact]
    public void Validate_WhenRequiredZapretFilesExist_ReturnsCompleteResult()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-runtime-test-");
        var layout = RuntimeLayout.ForDirectory(tempDir.FullName);
        layout.EnsureDirectories();
        Directory.CreateDirectory(Path.Combine(layout.RuntimeDirectory, "bin"));
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "winws.exe"), string.Empty);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "WinDivert64.sys"), string.Empty);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "WinDivert.dll"), string.Empty);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "service.bat"), string.Empty);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "general.bat"), string.Empty);

        var result = RuntimeValidator.Validate(layout.RuntimeDirectory);

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingItems);
    }
}
