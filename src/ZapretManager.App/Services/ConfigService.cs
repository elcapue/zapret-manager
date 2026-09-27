using System.Text.Json;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;

    public ConfigService(string configPath)
    {
        _configPath = configPath;
    }

    public AppConfig LoadOrCreate()
    {
        if (!File.Exists(_configPath))
        {
            var config = new AppConfig();
            Save(config);
            return config;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var loadedConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            Normalize(loadedConfig);
            var canonicalJson = JsonSerializer.Serialize(loadedConfig, JsonOptions);
            if (!string.Equals(json, canonicalJson, StringComparison.Ordinal))
            {
                Save(loadedConfig);
            }

            return loadedConfig;
        }
        catch (JsonException)
        {
            BackupInvalidConfig();
            var config = new AppConfig();
            Save(config);
            return config;
        }
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var temporaryPath = _configPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _configPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void SaveLastSuccessfulStrategyScan(
        AppConfig config,
        StrategyAutoSelectionResult result,
        DateTimeOffset scannedAtUtc)
    {
        if (!result.IsSuccess)
        {
            return;
        }

        config.LastStrategyScan = new LastStrategyScanResult
        {
            ScannedAtUtc = scannedAtUtc.ToUniversalTime(),
            Strategies = result.TopStrategies
                .Take(3)
                .Select(summary => new LastStrategyScanSummary
                {
                    StrategyFileName = summary.Strategy.FileName,
                    HttpOk = summary.HttpOk,
                    HttpError = summary.HttpError,
                    YouTubeHttpOk = summary.YouTubeHttpOk,
                    YouTubeHttpAttempts = summary.YouTubeHttpAttempts,
                    DiscordHttpOk = summary.DiscordHttpOk,
                    DiscordHttpAttempts = summary.DiscordHttpAttempts,
                    MedianLatencyMs = summary.MedianLatencyMs
                })
                .ToList()
        };
        Save(config);
    }

    private static void Normalize(AppConfig config)
    {
        config.SelectedStrategy = NormalizeOptionalText(config.SelectedStrategy);
        if (config.SelectedStrategy is not null)
        {
            config.SelectedStrategy = Path.GetFileName(config.SelectedStrategy);
        }

        config.LastKnownVersion = NormalizeOptionalText(config.LastKnownVersion);
        if (config.ManagedProcess is { ProcessId: <= 0 } ||
            string.IsNullOrWhiteSpace(config.ManagedProcess?.ExecutablePath))
        {
            config.ManagedProcess = null;
        }
        else if (config.ManagedProcess is not null)
        {
            config.ManagedProcess.StrategyFileName =
                Path.GetFileName(config.ManagedProcess.StrategyFileName) ?? string.Empty;
        }

        if (config.LastStrategyScan is null)
        {
            return;
        }

        config.LastStrategyScan.Strategies ??= [];
        config.LastStrategyScan.Strategies = config.LastStrategyScan.Strategies
            .Where(summary => !string.IsNullOrWhiteSpace(summary.StrategyFileName))
            .Take(3)
            .Select(summary =>
            {
                summary.StrategyFileName = Path.GetFileName(summary.StrategyFileName) ?? string.Empty;
                summary.HttpOk = Math.Max(summary.HttpOk, 0);
                summary.HttpError = Math.Max(summary.HttpError, 0);
                summary.YouTubeHttpOk = Math.Max(summary.YouTubeHttpOk, 0);
                summary.YouTubeHttpAttempts = Math.Max(summary.YouTubeHttpAttempts, 0);
                summary.DiscordHttpOk = Math.Max(summary.DiscordHttpOk, 0);
                summary.DiscordHttpAttempts = Math.Max(summary.DiscordHttpAttempts, 0);
                summary.MedianLatencyMs = Math.Max(summary.MedianLatencyMs, 0);
                return summary;
            })
            .ToList();
    }

    private void BackupInvalidConfig()
    {
        var backupPath = _configPath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        File.Move(_configPath, backupPath);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
