using ZapretManager.App.Core;
using ZapretManager.App.Services;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class AutoSelectionPresentationTests
{
    [Fact]
    public void Summary_WhenCompleted_ShowsResultAndShareOfPassedChecksOnly()
    {
        var best = new StrategyTestSummary(
            new StrategyInfo("general (ALT3).bat", "C:/runtime/general (ALT3).bat", false),
            HttpOk: 34,
            HttpError: 2,
            YouTubeHttpOk: 12,
            YouTubeHttpAttempts: 12,
            DiscordHttpOk: 11,
            DiscordHttpAttempts: 12,
            MedianLatencyMs: 140);
        var result = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.Completed,
            best.Strategy,
            [best],
            "Лучшая стратегия: general (ALT3).",
            ["Без zapret не открываются 5 из 12 сайтов: youtube.com."]);

        var summary = TrayApplicationContext.BuildStrategyAutoSelectionSummary(result);

        Assert.Equal(
            "Автовыбор завершён. Лучшая стратегия: general (ALT3)." + Environment.NewLine + "Прошли 34 из 36 проверок.",
            summary);
    }

    [Fact]
    public void Summary_WhenNothingHelps_ExplainsWithoutNumbers()
    {
        var result = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.NoWorkingStrategy,
            null,
            [],
            "Ни одна стратегия не улучшила доступ по сравнению с работой без zapret.");

        Assert.Equal(
            "Автовыбор завершён. Ни одна стратегия не улучшила доступ по сравнению с работой без zapret.",
            TrayApplicationContext.BuildStrategyAutoSelectionSummary(result));
    }

    [Fact]
    public void ToolTip_LaysOutTitleAndTwoColumnRowsIntoReadableBlock()
    {
        using var toolTip = new ThemedToolTip();

        var single = toolTip.Measure("Настройки");
        var details = toolTip.Measure("general\nПрошли\t31 из 36\nYouTube\t10 из 12\nДругие сайты\t12 из 12");
        var longNote = toolTip.Measure("Итоги проверки\n• " + string.Join(' ', Enumerable.Repeat("очень длинное наблюдение", 20)));

        Assert.True(details.Height > single.Height * 2);
        Assert.True(longNote.Width <= 380, "Длинные наблюдения переносятся, а не растягивают подсказку на весь экран.");
        Assert.True(longNote.Height > details.Height);
    }
}
