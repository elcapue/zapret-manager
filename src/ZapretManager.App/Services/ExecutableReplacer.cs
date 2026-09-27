namespace ZapretManager.App.Services;

/// <summary>
/// Заменяет exe менеджера, даже если он сейчас запущен: Windows не даёт удалить или перезаписать
/// работающий exe, но разрешает его переименовать. Старый файл уходит в «.old» и удаляется при
/// следующем запуске новой версии.
/// </summary>
public static class ExecutableReplacer
{
    private const string PreviousSuffix = ".old";

    /// <returns>Куда переложен прежний exe (для <see cref="RollBack"/>), или null, если его не было.</returns>
    public static string? Replace(string newExecutable, string targetExecutable)
    {
        DeleteLeftover(targetExecutable);
        string? previous = null;
        if (File.Exists(targetExecutable))
        {
            // Прошлый «.old» может быть ещё занят процессом, который только что завершается.
            previous = targetExecutable + PreviousSuffix;
            if (File.Exists(previous))
            {
                previous += "-" + Guid.NewGuid().ToString("N");
            }

            File.Move(targetExecutable, previous);
        }

        try
        {
            File.Copy(newExecutable, targetExecutable, overwrite: false);
        }
        catch
        {
            RollBack(targetExecutable, previous);
            throw;
        }

        return previous;
    }

    /// <summary>Возвращает прежний exe на место, если новая версия не установилась или не запустилась.</summary>
    public static void RollBack(string targetExecutable, string? previous)
    {
        if (previous is null || !File.Exists(previous))
        {
            return;
        }

        TryDelete(targetExecutable);
        File.Move(previous, targetExecutable);
    }

    public static void DeleteLeftover(string targetExecutable)
    {
        var directory = Path.GetDirectoryName(targetExecutable);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, Path.GetFileName(targetExecutable) + PreviousSuffix + "*"))
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Файл ещё занят завершающимся процессом — удалится при следующем запуске.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
