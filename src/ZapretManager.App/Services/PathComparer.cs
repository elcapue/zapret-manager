namespace ZapretManager.App.Services;

public static class PathComparer
{
    public static bool AreEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) when (left.Length > 0 && right.Length > 0)
        {
            return false;
        }
    }
}
