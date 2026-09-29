using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class WinwsProcessInspectorTests
{
    private static readonly DateTime StartedAt = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static readonly WinwsProcessInfo Expected = new(100, StartedAt, "C:/runtime/bin/winws.exe");

    [Fact]
    public void MatchesExpectedProcess_WhenStartTimeAndPathMatch_ReturnsTrue()
    {
        var matches = WinwsProcessInspector.MatchesExpectedProcess(
            StartedAt, "c:/runtime/bin/winws.exe", Expected, out var errorMessage);

        Assert.True(matches);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    public void MatchesExpectedProcess_WhenPidWasReused_ReturnsFalse()
    {
        var matches = WinwsProcessInspector.MatchesExpectedProcess(
            StartedAt.AddMinutes(1), Expected.ExecutablePath, Expected, out var errorMessage);

        Assert.False(matches);
        Assert.Equal("PID уже принадлежит другому процессу.", errorMessage);
    }

    [Fact]
    public void MatchesExpectedProcess_WhenPathDiffers_ReturnsFalse()
    {
        var matches = WinwsProcessInspector.MatchesExpectedProcess(
            StartedAt, "C:/other/bin/winws.exe", Expected, out var errorMessage);

        Assert.False(matches);
        Assert.Equal("Путь процесса не совпадает с сохранённым runtime.", errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MatchesExpectedProcess_WhenPathUnknown_DoesNotKill(string? actualPath)
    {
        var matches = WinwsProcessInspector.MatchesExpectedProcess(
            StartedAt, actualPath, Expected, out var errorMessage);

        Assert.False(matches);
        Assert.Equal("Путь процесса не совпадает с сохранённым runtime.", errorMessage);
    }
}
