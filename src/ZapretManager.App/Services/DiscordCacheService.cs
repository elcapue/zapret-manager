using System.Diagnostics;

namespace ZapretManager.App.Services;

public sealed record DiscordCacheClearResult(
    IReadOnlyList<string> Cleared,
    IReadOnlyList<string> Failures)
{
    public bool FoundAny => Cleared.Count + Failures.Count > 0;

    public bool IsSuccess => FoundAny && Failures.Count == 0;

    public string Message => !FoundAny
        ? "Discord не найден."
        : Failures.Count == 0
            ? $"Кеш очищен: {string.Join(", ", Cleared)}. Запустите Discord заново."
            : "Не удалось очистить кеш Discord полностью:\n" + string.Join("\n", Failures);
}

/// <summary>
/// Очистка кеша Discord — то же, что делает Flowseal service.bat: закрыть клиент и удалить
/// Cache, Code Cache и GPUCache. Настройки, вход в аккаунт и локальные данные не затрагиваются.
/// После смены стратегии это помогает Discord, который «завис» на старых ответах.
/// </summary>
public sealed class DiscordCacheService
{
    internal static readonly IReadOnlyList<DiscordInstallation> Installations =
    [
        new("discord", "Discord", "Discord"),
        new("discordptb", "DiscordPTB", "Discord PTB"),
        new("discordcanary", "DiscordCanary", "Discord Canary"),
        new("discorddevelopment", "DiscordDevelopment", "Discord Development")
    ];

    internal static readonly IReadOnlyList<string> CacheFolders = ["Cache", "Code Cache", "GPUCache"];

    private readonly string _appDataDirectory;
    private readonly Func<string, bool> _closeProcesses;
    private readonly TimeSpan _retryDelay;

    public DiscordCacheService()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CloseProcesses, TimeSpan.FromMilliseconds(500))
    {
    }

    internal DiscordCacheService(string appDataDirectory, Func<string, bool> closeProcesses, TimeSpan retryDelay)
    {
        _appDataDirectory = appDataDirectory;
        _closeProcesses = closeProcesses;
        _retryDelay = retryDelay;
    }

    public IReadOnlyList<string> FindInstalled()
    {
        return Installations
            .Where(installation => Directory.Exists(GetDataDirectory(installation)))
            .Select(installation => installation.DisplayName)
            .ToArray();
    }

    public DiscordCacheClearResult Clear()
    {
        var cleared = new List<string>();
        var failures = new List<string>();
        foreach (var installation in Installations)
        {
            var dataDirectory = GetDataDirectory(installation);
            if (!Directory.Exists(dataDirectory))
            {
                continue;
            }

            if (!_closeProcesses(installation.ProcessName))
            {
                failures.Add($"{installation.DisplayName}: не удалось закрыть клиент.");
                continue;
            }

            var failedFolders = CacheFolders
                .Select(folder => Path.Combine(dataDirectory, folder))
                .Where(path => !TryDeleteDirectory(path))
                .Select(Path.GetFileName)
                .ToArray();
            if (failedFolders.Length == 0)
            {
                cleared.Add(installation.DisplayName);
            }
            else
            {
                failures.Add($"{installation.DisplayName}: не удалось удалить {string.Join(", ", failedFolders)}.");
            }
        }

        return new DiscordCacheClearResult(cleared, failures);
    }

    private string GetDataDirectory(DiscordInstallation installation)
    {
        return Path.Combine(_appDataDirectory, installation.DataFolder);
    }

    /// <summary>После закрытия Discord файлы кеша освобождаются не мгновенно — несколько попыток.</summary>
    private bool TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (_retryDelay > TimeSpan.Zero)
                {
                    Thread.Sleep(_retryDelay);
                }
            }
        }

        return !Directory.Exists(path);
    }

    private static bool CloseProcesses(string processName)
    {
        var allClosed = true;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    allClosed &= process.WaitForExit(10_000);
                }
                catch (InvalidOperationException)
                {
                    // Процесс уже завершился.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    allClosed = false;
                }
            }
        }

        return allClosed;
    }

    internal sealed record DiscordInstallation(string DataFolder, string ProcessName, string DisplayName);
}
