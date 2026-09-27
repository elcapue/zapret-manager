namespace ZapretManager.App.Services;

public enum IpsetMode
{
    /// <summary>IP-адреса проверяются по списку: в ipset-all.txt лежит сам список.</summary>
    Loaded,

    /// <summary>Ни один адрес не попадает под фильтр: в ipset-all.txt только заглушка.</summary>
    None,

    /// <summary>Под фильтр попадает любой адрес: ipset-all.txt пустой.</summary>
    Any
}

/// <summary>
/// Режим «IPSet Filter» из service.bat Flowseal. Отдельной настройки у него нет: режим задаётся
/// содержимым lists/ipset-all.txt, а список, пока режим не «loaded», лежит в ipset-all.txt.backup.
/// Релиз Flowseal поставляется в режиме «none», поэтому при обновлении runtime режим пользователя
/// нужно вернуть, взяв при этом свежий список из релиза.
/// </summary>
public static class IpsetListMode
{
    internal const string NonePlaceholder = "203.0.113.113/32";

    public static IpsetMode? Detect(string runtimeDirectory)
    {
        var listPath = GetListPath(runtimeDirectory);
        return File.Exists(listPath) ? DetectFile(listPath) : null;
    }

    /// <summary>Переводит только что установленный runtime в режим <paramref name="mode"/>, сохраняя список из релиза.</summary>
    public static void Apply(string runtimeDirectory, IpsetMode mode)
    {
        var listPath = GetListPath(runtimeDirectory);
        var backupPath = listPath + ".backup";
        var releaseListPath = File.Exists(backupPath)
            ? backupPath
            : File.Exists(listPath) && DetectFile(listPath) == IpsetMode.Loaded ? listPath : null;
        if (releaseListPath is null)
        {
            return;
        }

        if (mode == IpsetMode.Loaded)
        {
            if (releaseListPath == backupPath)
            {
                File.Move(backupPath, listPath, overwrite: true);
            }

            return;
        }

        if (releaseListPath == listPath)
        {
            File.Move(listPath, backupPath, overwrite: true);
        }

        File.WriteAllText(listPath, mode == IpsetMode.None ? NonePlaceholder + "\r\n" : string.Empty);
    }

    // Так же, как :ipset_switch_status в service.bat: пустой файл — any, заглушка — none, иначе loaded.
    private static IpsetMode DetectFile(string listPath)
    {
        if (new FileInfo(listPath).Length == 0)
        {
            return IpsetMode.Any;
        }

        return File.ReadLines(listPath).Any(line => line.Contains(NonePlaceholder, StringComparison.Ordinal))
            ? IpsetMode.None
            : IpsetMode.Loaded;
    }

    private static string GetListPath(string runtimeDirectory)
    {
        return Path.Combine(runtimeDirectory, "lists", "ipset-all.txt");
    }
}
