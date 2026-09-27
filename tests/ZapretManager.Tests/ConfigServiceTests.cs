using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public void LoadOrCreate_WhenFileDoesNotExist_CreatesDefaultConfigFile()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var service = new ConfigService(configPath);

        var config = service.LoadOrCreate();

        Assert.Null(config.SelectedStrategy);
        Assert.Null(config.LastKnownVersion);
        Assert.Null(config.LastStrategyScan);
        Assert.False(config.CheckForUpdatesOnStartup);
        Assert.False(config.StartWithWindows);
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public void Save_WhenConfigChanges_PersistsValuesForNextLoad()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var service = new ConfigService(configPath);
        var config = new AppConfig
        {
            SelectedStrategy = "general (ALT).bat",
            LastKnownVersion = "1.10.1",
            StartWithWindows = true,
            ManagedProcess = new ManagedProcessState
            {
                ProcessId = 1234,
                StartedAtUtc = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
                ExecutablePath = "D:/Zapret/runtime/bin/winws.exe",
                StrategyFileName = "general (ALT).bat"
            }
        };

        service.Save(config);
        var reloaded = service.LoadOrCreate();

        Assert.Equal("general (ALT).bat", reloaded.SelectedStrategy);
        Assert.Equal("1.10.1", reloaded.LastKnownVersion);
        Assert.Equal(1234, reloaded.ManagedProcess?.ProcessId);
        Assert.Equal("D:/Zapret/runtime/bin/winws.exe", reloaded.ManagedProcess?.ExecutablePath);
        Assert.True(reloaded.StartWithWindows);
    }

    [Fact]
    public void LoadOrCreate_DropsUnknownFieldsAndKeepsKnownOnes()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        File.WriteAllText(configPath, """
            {
              "UnknownFlag": true,
              "UnknownPath": "D:/somewhere",
              "CheckForUpdatesOnStartup": true
            }
            """);
        var service = new ConfigService(configPath);

        var config = service.LoadOrCreate();

        Assert.True(config.CheckForUpdatesOnStartup);

        var rewrittenJson = File.ReadAllText(configPath);
        Assert.DoesNotContain("UnknownFlag", rewrittenJson);
        Assert.DoesNotContain("UnknownPath", rewrittenJson);
    }

    [Fact]
    public void SaveLastSuccessfulStrategyScan_PersistsTimeAndAtMostThreeSummaries()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var service = new ConfigService(Path.Combine(tempDir.FullName, "config.json"));
        var config = service.LoadOrCreate();
        var scannedAt = new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.Zero);
        var summaries = Enumerable.Range(1, 4)
            .Select(index => new StrategyTestSummary(
                new StrategyInfo($"general{index}.bat", $"D:/runtime/general{index}.bat", false),
                HttpOk: 10 - index,
                HttpError: index,
                YouTubeHttpOk: 6 - index,
                YouTubeHttpAttempts: 6,
                DiscordHttpOk: 7 - index,
                DiscordHttpAttempts: 6,
                MedianLatencyMs: 100 * index))
            .ToArray();
        var result = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.Completed,
            summaries[0].Strategy,
            summaries,
            "done");

        service.SaveLastSuccessfulStrategyScan(config, result, scannedAt);
        var reloaded = service.LoadOrCreate();

        Assert.Equal(scannedAt, reloaded.LastStrategyScan?.ScannedAtUtc);
        Assert.Equal(3, reloaded.LastStrategyScan?.Strategies.Count);
        var first = Assert.IsType<LastStrategyScanSummary>(reloaded.LastStrategyScan?.Strategies[0]);
        Assert.Equal("general1.bat", first.StrategyFileName);
        Assert.Equal(9, first.HttpOk);
        Assert.Equal(1, first.HttpError);
        Assert.Equal(5, first.YouTubeHttpOk);
        Assert.Equal(6, first.YouTubeHttpAttempts);
        Assert.Equal(6, first.DiscordHttpOk);
        Assert.Equal(6, first.DiscordHttpAttempts);
        Assert.Equal(100, first.MedianLatencyMs);
    }

    [Fact]
    public void SaveLastSuccessfulStrategyScan_WhenScanFails_KeepsPreviousResult()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var service = new ConfigService(Path.Combine(tempDir.FullName, "config.json"));
        var previous = new LastStrategyScanResult
        {
            ScannedAtUtc = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero),
            Strategies = [new LastStrategyScanSummary { StrategyFileName = "general.bat", HttpOk = 8 }]
        };
        var config = new AppConfig { LastStrategyScan = previous };
        service.Save(config);
        var failed = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.NoWorkingStrategy,
            null,
            Array.Empty<StrategyTestSummary>(),
            "failed");

        service.SaveLastSuccessfulStrategyScan(config, failed, DateTimeOffset.UtcNow);
        var reloaded = service.LoadOrCreate();

        Assert.Equal(previous.ScannedAtUtc, reloaded.LastStrategyScan?.ScannedAtUtc);
        Assert.Equal("general.bat", reloaded.LastStrategyScan?.Strategies.Single().StrategyFileName);
    }

    [Fact]
    public void LoadOrCreate_WhenJsonIsInvalid_BacksItUpAndCreatesSafeDefaults()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-config-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        File.WriteAllText(configPath, "{ invalid json");
        var service = new ConfigService(configPath);

        var config = service.LoadOrCreate();

        Assert.Null(config.SelectedStrategy);
        Assert.Null(config.LastKnownVersion);
        Assert.Null(config.ManagedProcess);
        Assert.False(config.CheckForUpdatesOnStartup);
        Assert.True(File.Exists(configPath));
        Assert.Single(Directory.GetFiles(tempDir.FullName, "config.json.invalid-*"));
        Assert.Empty(Directory.GetFiles(tempDir.FullName, "*.tmp-*"));
    }
}
