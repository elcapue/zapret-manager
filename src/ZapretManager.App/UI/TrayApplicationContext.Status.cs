using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    /// <summary>Подпись в переключателе: время работы и стратегия, выбранная стратегия или пояснение.</summary>
    private string GetStatusHint()
    {
        switch (_state)
        {
            case ZapretState.Running when _config.ManagedProcess is { } process:
                var uptime = FormatUptime(DateTime.UtcNow - process.StartedAtUtc);
                return string.IsNullOrWhiteSpace(process.StrategyFileName)
                    ? uptime
                    : uptime + " · " + Path.GetFileNameWithoutExtension(process.StrategyFileName);
            case ZapretState.Stopped:
                return string.IsNullOrWhiteSpace(_config.SelectedStrategy)
                    ? "Стратегия не выбрана"
                    : Path.GetFileNameWithoutExtension(_config.SelectedStrategy);
            case ZapretState.External:
                return "запущен не менеджером";
            default:
                return string.Empty;
        }
    }

    /// <summary>Версия установленного runtime для шапки окна.</summary>
    private string GetRuntimeVersionText()
    {
        if (_state == ZapretState.RuntimeMissing)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(_config.LastKnownVersion)
            ? "Flowseal: версия неизвестна"
            : "Flowseal " + _config.LastKnownVersion.Trim().TrimStart('v', 'V');
    }

    /// <summary>Компактное время работы: «08:28», «1:08:28», «2 д 03:15».</summary>
    internal static string FormatUptime(TimeSpan uptime)
    {
        var normalized = uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
        if (normalized.TotalDays >= 1)
        {
            return $"{(int)normalized.TotalDays} д {normalized.Hours:00}:{normalized.Minutes:00}";
        }

        return normalized.TotalHours >= 1
            ? $"{normalized.Hours}:{normalized.Minutes:00}:{normalized.Seconds:00}"
            : $"{normalized.Minutes:00}:{normalized.Seconds:00}";
    }
}
