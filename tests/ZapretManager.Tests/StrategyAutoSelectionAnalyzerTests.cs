using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class StrategyAutoSelectionAnalyzerTests
{
    [Fact]
    public void TryCreate_WhenValueIsUrl_PreservesUrlHostAndGroup()
    {
        var target = StrategyTestTarget.TryCreate(
            "Discord CDN",
            "https://cdn.discordapp.com/assets/app.js",
            StrategyTargetGroup.Discord);

        Assert.NotNull(target);
        Assert.Equal("Discord CDN", target.Name);
        Assert.Equal("https://cdn.discordapp.com/assets/app.js", target.Url.ToString());
        Assert.Equal("cdn.discordapp.com", target.Host);
        Assert.Equal(StrategyTargetGroup.Discord, target.Group);
    }

    [Theory]
    [InlineData("PING:1.1.1.1")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void TryCreate_WhenValueIsNotHttpUrl_ReturnsNull(string value)
    {
        Assert.Null(StrategyTestTarget.TryCreate("Target", value));
    }

    [Fact]
    public void GetDefaultTargets_ReturnsFlowsealHttpTargetsWithExplicitGroups()
    {
        var targets = StrategyTestTarget.GetDefaultTargets();

        Assert.Contains(targets, target => target.Name == "Discord Main" && target.Group == StrategyTargetGroup.Discord);
        Assert.Contains(targets, target => target.Name == "YouTube Web" && target.Group == StrategyTargetGroup.YouTube);
        Assert.Equal(12, targets.Count);
        Assert.Equal(4, targets.Count(target => target.Group == StrategyTargetGroup.Discord));
        Assert.Equal(4, targets.Count(target => target.Group == StrategyTargetGroup.YouTube));
    }

    [Fact]
    public void TargetCatalog_ParseUsesSectionHeadingsAndSkipsPingTargets()
    {
        var targets = StrategyTargetCatalog.Parse(
        [
            "### Discord",
            "Main = \"https://discord.com\"",
            "### YouTube",
            "Video = \"https://www.youtube.com\"",
            "### Other",
            "Discord-looking name = \"https://example.com\"",
            "DNS = \"PING:1.1.1.1\""
        ]);

        Assert.Equal(3, targets.Count);
        Assert.Equal(StrategyTargetGroup.Discord, targets[0].Group);
        Assert.Equal(StrategyTargetGroup.YouTube, targets[1].Group);
        Assert.Equal(StrategyTargetGroup.Baseline, targets[2].Group);
    }

    [Fact]
    public void Rank_PrefersHigherSuccessRateOverServiceBalance()
    {
        var oneSided = Summary("general.bat", ytOk: 6, ytAttempts: 6, dcOk: 3, dcAttempts: 6);
        var balanced = Summary("general (ALT).bat", ytOk: 4, ytAttempts: 6, dcOk: 4, dcAttempts: 6);

        Assert.Equal("general.bat", StrategyAutoSelectionAnalyzer.Rank([balanced, oneSided])[0].Strategy.FileName);
    }

    [Fact]
    public void Rank_WhenSuccessRateTies_PrefersBalancedServices()
    {
        var oneSided = Summary("general.bat", ytOk: 6, ytAttempts: 6, dcOk: 2, dcAttempts: 6);
        var balanced = Summary("general (ALT).bat", ytOk: 4, ytAttempts: 6, dcOk: 4, dcAttempts: 6);

        Assert.Equal("general (ALT).bat", StrategyAutoSelectionAnalyzer.Rank([oneSided, balanced])[0].Strategy.FileName);
    }

    [Fact]
    public void Rank_WhenQualityTies_PrefersLowerLatencyInsteadOfName()
    {
        var slow = Summary("general.bat", 6, 6, 6, 6, latencyMs: 400);
        var fast = Summary("general (ALT9).bat", 6, 6, 6, 6, latencyMs: 120);

        var ranked = StrategyAutoSelectionAnalyzer.Rank([slow, fast]);

        Assert.Equal("general (ALT9).bat", ranked[0].Strategy.FileName);
        Assert.True(StrategyAutoSelectionAnalyzer.HaveSameScore(ranked[0], ranked[1]));
    }

    [Fact]
    public void Rank_ComparesSuccessRateNotRawCounts()
    {
        // Три раунда перепроверки дают больше попыток, но сравнивать нужно долю успеха.
        var confirmed = Summary("general.bat", ytOk: 15, ytAttempts: 18, dcOk: 15, dcAttempts: 18);
        var single = Summary("general (ALT).bat", ytOk: 6, ytAttempts: 6, dcOk: 6, dcAttempts: 6);

        Assert.Equal("general (ALT).bat", StrategyAutoSelectionAnalyzer.Rank([confirmed, single])[0].Strategy.FileName);
    }

    [Fact]
    public void Rank_PutsStrategiesThatFailedToStartLast()
    {
        var notStarted = StrategyTestSummary.NotStarted(new StrategyInfo("general.bat", "C:/runtime/general.bat", false));
        var weak = Summary("general (ALT).bat", ytOk: 0, ytAttempts: 6, dcOk: 0, dcAttempts: 6);

        Assert.Equal([weak, notStarted], StrategyAutoSelectionAnalyzer.Rank([notStarted, weak]));
    }

    [Fact]
    public void FromAttempts_IgnoresDnsAndCertificateFailuresAndComputesMedianLatency()
    {
        var yt = StrategyTestTarget.TryCreate("YT", "https://yt.example", StrategyTargetGroup.YouTube)!;
        var dc = StrategyTestTarget.TryCreate("DC", "https://dc.example", StrategyTargetGroup.Discord)!;
        var summary = StrategyTestSummary.FromAttempts(
            new StrategyInfo("general.bat", "C:/runtime/general.bat", false),
            [
                new ProbeAttempt(yt, ProbeOutcome.Ok, TimeSpan.FromMilliseconds(100)),
                new ProbeAttempt(yt, ProbeOutcome.Ok, TimeSpan.FromMilliseconds(300)),
                new ProbeAttempt(yt, ProbeOutcome.DnsFailure, TimeSpan.Zero),
                new ProbeAttempt(dc, ProbeOutcome.Failed, TimeSpan.FromMilliseconds(3000)),
                new ProbeAttempt(dc, ProbeOutcome.CertificateError, TimeSpan.Zero)
            ]);

        Assert.Equal(2, summary.HttpOk);
        Assert.Equal(1, summary.HttpError);
        Assert.Equal(2, summary.YouTubeHttpAttempts);
        Assert.Equal(1, summary.DiscordHttpAttempts);
        Assert.Equal(200, summary.MedianLatencyMs);
    }

    private static StrategyTestSummary Summary(
        string fileName,
        int ytOk,
        int ytAttempts,
        int dcOk,
        int dcAttempts,
        int latencyMs = 100)
    {
        return new StrategyTestSummary(
            new StrategyInfo(fileName, "C:/runtime/" + fileName, false),
            HttpOk: ytOk + dcOk,
            HttpError: ytAttempts + dcAttempts - ytOk - dcOk,
            YouTubeHttpOk: ytOk,
            YouTubeHttpAttempts: ytAttempts,
            DiscordHttpOk: dcOk,
            DiscordHttpAttempts: dcAttempts,
            MedianLatencyMs: latencyMs);
    }
}
