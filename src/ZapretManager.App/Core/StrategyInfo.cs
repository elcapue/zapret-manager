namespace ZapretManager.App.Core;

public sealed class StrategyInfo
{
    public StrategyInfo(string fileName, string fullPath, bool isSelected)
    {
        FileName = fileName;
        DisplayName = Path.GetFileNameWithoutExtension(fileName);
        FullPath = fullPath;
        IsSelected = isSelected;
    }

    public string FileName { get; }

    public string DisplayName { get; }

    public string FullPath { get; }

    public bool IsSelected { get; }
}
