namespace ZapretManager.App.Services;

public sealed class RuntimeValidationResult
{
    public RuntimeValidationResult(IReadOnlyList<string> missingItems)
    {
        MissingItems = missingItems;
    }

    public IReadOnlyList<string> MissingItems { get; }

    public bool IsComplete => MissingItems.Count == 0;
}

public static class RuntimeValidator
{
    public static RuntimeValidationResult Validate(string runtimeDirectory)
    {
        var missingItems = new List<string>();

        AddIfMissing(missingItems, runtimeDirectory, "bin/winws.exe");
        AddIfMissing(missingItems, runtimeDirectory, "bin/WinDivert64.sys");
        AddIfMissing(missingItems, runtimeDirectory, "bin/WinDivert.dll");
        AddIfMissing(missingItems, runtimeDirectory, "service.bat");

        if (!Directory.Exists(runtimeDirectory) || Directory.GetFiles(runtimeDirectory, StrategyService.StrategyFilePattern).Length == 0)
        {
            missingItems.Add("general*.bat");
        }

        return new RuntimeValidationResult(missingItems);
    }

    private static void AddIfMissing(ICollection<string> missingItems, string runtimeDirectory, string relativePath)
    {
        var path = Path.Combine(runtimeDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            missingItems.Add(relativePath);
        }
    }
}
