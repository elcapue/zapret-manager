namespace ZapretManager.App.Services;

public static class StrategyTargetCatalog
{
    public static IReadOnlyList<StrategyTestTarget> Load(string runtimeDirectory)
    {
        var path = Path.Combine(runtimeDirectory, "utils", "targets.txt");
        if (!File.Exists(path))
        {
            return StrategyTestTarget.GetDefaultTargets();
        }

        try
        {
            var parsed = Parse(File.ReadLines(path));
            return HasRequiredServiceGroups(parsed)
                ? parsed
                : StrategyTestTarget.GetDefaultTargets();
        }
        catch (IOException)
        {
            return StrategyTestTarget.GetDefaultTargets();
        }
        catch (UnauthorizedAccessException)
        {
            return StrategyTestTarget.GetDefaultTargets();
        }
    }

    internal static IReadOnlyList<StrategyTestTarget> Parse(IEnumerable<string> lines)
    {
        var targets = new List<StrategyTestTarget>();
        var group = StrategyTargetGroup.Baseline;

        foreach (var sourceLine in lines)
        {
            var line = sourceLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                var parsedHeading = ParseHeading(line);
                if (parsedHeading is not null)
                {
                    group = parsedHeading.Value;
                }

                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            var target = name.Length == 0 ? null : StrategyTestTarget.TryCreate(name, value, group);
            if (target is not null)
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private static StrategyTargetGroup? ParseHeading(string heading)
    {
        if (heading.Contains("Discord", StringComparison.OrdinalIgnoreCase))
        {
            return StrategyTargetGroup.Discord;
        }

        if (heading.Contains("YouTube", StringComparison.OrdinalIgnoreCase))
        {
            return StrategyTargetGroup.YouTube;
        }

        return heading.StartsWith("###", StringComparison.Ordinal)
            ? StrategyTargetGroup.Baseline
            : null;
    }

    private static bool HasRequiredServiceGroups(IReadOnlyList<StrategyTestTarget> targets)
    {
        return targets.Any(target => target.Group == StrategyTargetGroup.Discord) &&
               targets.Any(target => target.Group == StrategyTargetGroup.YouTube);
    }
}
