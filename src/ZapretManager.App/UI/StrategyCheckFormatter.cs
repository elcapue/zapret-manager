using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

internal static class StrategyCheckFormatter
{
    public static StrategyResultsList.ResultRow ToRow(StrategyTestSummary summary)
    {
        return ToRow(
            summary.Strategy.FileName,
            summary.HttpOk,
            summary.HttpError,
            summary.YouTubeHttpOk,
            summary.YouTubeHttpAttempts,
            summary.DiscordHttpOk,
            summary.DiscordHttpAttempts,
            summary.MedianLatencyMs);
    }

    public static StrategyResultsList.ResultRow ToRow(LastStrategyScanSummary summary)
    {
        return ToRow(
            summary.StrategyFileName,
            summary.HttpOk,
            summary.HttpError,
            summary.YouTubeHttpOk,
            summary.YouTubeHttpAttempts,
            summary.DiscordHttpOk,
            summary.DiscordHttpAttempts,
            summary.MedianLatencyMs);
    }

    /// <summary>Коротко для строки списка: доля успешных проверок и задержка.</summary>
    public static string FormatMetric(int httpOk, int httpError, int medianLatencyMs = 0)
    {
        var total = TotalAttempts(httpOk, httpError);
        if (total == 0)
        {
            return "нет данных";
        }

        var metric = $"{(int)Math.Round(Math.Max(0, httpOk) * 100.0 / total)}%";
        return medianLatencyMs > 0 ? $"{metric} · {medianLatencyMs} мс" : metric;
    }

    /// <summary>Подробности для подсказки (разметка <see cref="ThemedToolTip"/>): заголовок и строки «метка\tзначение».</summary>
    public static string FormatDetails(
        string title,
        int httpOk,
        int httpError,
        int youTubeHttpOk,
        int youTubeHttpAttempts,
        int discordHttpOk,
        int discordHttpAttempts,
        int medianLatencyMs = 0)
    {
        var totalAttempts = TotalAttempts(httpOk, httpError);
        var otherAttempts = Math.Max(0, totalAttempts - youTubeHttpAttempts - discordHttpAttempts);
        var otherOk = Math.Max(0, httpOk - youTubeHttpOk - discordHttpOk);
        var lines = new List<string>
        {
            title,
            $"Прошли\t{FormatGroup(httpOk, totalAttempts)}",
            $"YouTube\t{FormatGroup(youTubeHttpOk, youTubeHttpAttempts)}",
            $"Discord\t{FormatGroup(discordHttpOk, discordHttpAttempts)}",
            $"Другие сайты\t{FormatGroup(otherOk, otherAttempts)}"
        };

        if (medianLatencyMs > 0)
        {
            lines.Add($"Задержка\t{medianLatencyMs} мс");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static StrategyResultsList.ResultRow ToRow(
        string fileName,
        int httpOk,
        int httpError,
        int youTubeHttpOk,
        int youTubeHttpAttempts,
        int discordHttpOk,
        int discordHttpAttempts,
        int medianLatencyMs)
    {
        return new StrategyResultsList.ResultRow(
            fileName,
            FormatMetric(httpOk, httpError, medianLatencyMs),
            FormatDetails(
                Path.GetFileNameWithoutExtension(fileName),
                httpOk,
                httpError,
                youTubeHttpOk,
                youTubeHttpAttempts,
                discordHttpOk,
                discordHttpAttempts,
                medianLatencyMs));
    }

    private static string FormatGroup(int successful, int attempts)
    {
        return attempts > 0
            ? $"{Math.Max(0, successful)} из {attempts}"
            : "не проверялось";
    }

    private static int TotalAttempts(int httpOk, int httpError)
    {
        return Math.Max(0, httpOk) + Math.Max(0, httpError);
    }
}
