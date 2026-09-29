using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class StrategyCheckFormatterTests
{
    [Theory]
    [InlineData(0, 0, "нет данных")]
    [InlineData(9, 1, "90%")]
    [InlineData(1, 0, "100%")]
    [InlineData(0, 4, "0%")]
    [InlineData(-3, -2, "нет данных")]
    public void FormatMetric_ComputesShareAndHandlesEmptyOrNegative(int httpOk, int httpError, string expected)
    {
        Assert.Equal(expected, StrategyCheckFormatter.FormatMetric(httpOk, httpError));
    }

    [Fact]
    public void FormatMetric_WithLatency_AppendsMedian()
    {
        Assert.Equal("94% · 140 мс", StrategyCheckFormatter.FormatMetric(94, 6, medianLatencyMs: 140));
    }

    [Fact]
    public void FormatMetric_WithoutLatency_ShowsOnlyShare()
    {
        Assert.Equal("94%", StrategyCheckFormatter.FormatMetric(94, 6));
    }

    [Fact]
    public void FormatDetails_WhenGroupNotChecked_ShowsPlaceholder()
    {
        var details = StrategyCheckFormatter.FormatDetails(
            title: "general (ALT2)",
            httpOk: 0, httpError: 0,
            youTubeHttpOk: 0, youTubeHttpAttempts: 0,
            discordHttpOk: 0, discordHttpAttempts: 0);

        var lines = details.Split(Environment.NewLine);
        Assert.Equal("general (ALT2)", lines[0]);
        Assert.Contains(lines, line => line == "YouTube\tне проверялось");
        Assert.Contains(lines, line => line == "Discord\tне проверялось");
        Assert.DoesNotContain(lines, line => line.StartsWith("Задержка"));
    }

    [Fact]
    public void FormatDetails_DerivesOtherSitesFromTotals()
    {
        // Всего 12 попыток, YouTube+Discord закрывают 10 — оставшиеся 2 считаются «другими сайтами».
        var details = StrategyCheckFormatter.FormatDetails(
            title: "general",
            httpOk: 11, httpError: 1,
            youTubeHttpOk: 5, youTubeHttpAttempts: 5,
            discordHttpOk: 4, discordHttpAttempts: 5,
            medianLatencyMs: 88);

        var lines = details.Split(Environment.NewLine);
        Assert.Contains(lines, line => line == "Прошли\t11 из 12");
        Assert.Contains(lines, line => line == "YouTube\t5 из 5");
        Assert.Contains(lines, line => line == "Discord\t4 из 5");
        Assert.Contains(lines, line => line == "Другие сайты\t2 из 2");
        Assert.Equal("Задержка\t88 мс", lines[^1]);
    }
}
