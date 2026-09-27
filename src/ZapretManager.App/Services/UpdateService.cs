using System.IO.Compression;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class UpdateService
{
    private static readonly string[] PreservedRelativePaths =
    [
        "lists/list-general-user.txt",
        "lists/list-exclude-user.txt",
        "lists/ipset-exclude-user.txt",
        "utils/game_filter.enabled",
        "utils/check_updates.enabled",
        "bin/ACTIVE_DISCORD_UDP.bin",
        "bin/ACTIVE_GAME_UDP.bin"
    ];

    private readonly HttpClient _httpClient;
    private readonly RuntimeLayout _layout;

    public UpdateService(HttpClient httpClient, RuntimeLayout layout)
    {
        _httpClient = httpClient;
        _layout = layout;
    }

    public async Task<UpdateApplyResult> ApplyUpdateAsync(
        GitHubReleaseInfo release,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        var prepareResult = await PrepareUpdateAsync(release, cancellationToken).ConfigureAwait(false);
        using var preparedUpdate = prepareResult.PreparedUpdate;
        return preparedUpdate is null
            ? new UpdateApplyResult(prepareResult.Status, prepareResult.Message)
            : ApplyPreparedUpdate(preparedUpdate, config);
    }

    public async Task<UpdatePrepareResult> PrepareUpdateAsync(
        GitHubReleaseInfo release,
        CancellationToken cancellationToken)
    {
        if (release.ZipAsset is null)
        {
            return new UpdatePrepareResult(
                UpdateApplyStatus.NoZipAsset,
                "В релизе не найден ZIP asset.",
                null);
        }

        string? workDirectory = null;
        try
        {
            _layout.EnsureDirectories();
            workDirectory = Path.Combine(_layout.TempDirectory, "update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectory);
            var zipPath = Path.Combine(workDirectory, release.ZipAsset.Name);
            var zipBytes = await _httpClient.GetByteArrayAsync(release.ZipAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(zipPath, zipBytes, cancellationToken).ConfigureAwait(false);

            if (!ReleaseDigest.Matches(zipBytes, release.ZipAsset.Digest))
            {
                return new UpdatePrepareResult(
                    UpdateApplyStatus.HashMismatch,
                    "SHA256 скачанного архива не совпал с digest релиза.",
                    null);
            }

            var extractDirectory = Path.Combine(workDirectory, SanitizePathSegment(release.TagName));
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory);

            var packageRoot = FindValidPackageRoot(extractDirectory);
            if (packageRoot is null)
            {
                return new UpdatePrepareResult(
                    UpdateApplyStatus.InvalidPackage,
                    "Архив не содержит обязательные файлы zapret.",
                    null);
            }

            var preparedUpdate = new PreparedUpdate(workDirectory, packageRoot, release);
            workDirectory = null;
            return new UpdatePrepareResult(
                UpdateApplyStatus.Applied,
                $"Обновление до {release.TagName} скачано и проверено.",
                preparedUpdate);
        }
        catch (Exception ex)
        {
            return new UpdatePrepareResult(UpdateApplyStatus.Failed, ex.Message, null);
        }
        finally
        {
            if (workDirectory is not null)
            {
                TryDeleteDirectoryIfExists(workDirectory);
            }
        }
    }

    public UpdateApplyResult ApplyPreparedUpdate(PreparedUpdate preparedUpdate, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(preparedUpdate);

        var preserveDirectory = Path.Combine(preparedUpdate.WorkDirectory, "preserve");
        try
        {
            PreserveUserFiles(preserveDirectory);
            var ipsetMode = IpsetListMode.Detect(_layout.RuntimeDirectory);

            var backupDirectory = Path.Combine(_layout.BackupsDirectory, "runtime-previous");
            DeleteDirectoryIfExists(backupDirectory);
            if (Directory.Exists(_layout.RuntimeDirectory) &&
                Directory.EnumerateFileSystemEntries(_layout.RuntimeDirectory).Any())
            {
                Directory.Move(_layout.RuntimeDirectory, backupDirectory);
            }
            else
            {
                DeleteDirectoryIfExists(_layout.RuntimeDirectory);
            }

            try
            {
                CopyDirectory(preparedUpdate.PackageRoot, _layout.RuntimeDirectory);
                RestoreUserFiles(preserveDirectory);
                if (ipsetMode is { } mode)
                {
                    IpsetListMode.Apply(_layout.RuntimeDirectory, mode);
                }

                config.LastKnownVersion = preparedUpdate.Release.TagName;
                return new UpdateApplyResult(
                    UpdateApplyStatus.Applied,
                    $"Обновление до {preparedUpdate.Release.TagName} установлено.");
            }
            catch (Exception ex)
            {
                DeleteDirectoryIfExists(_layout.RuntimeDirectory);
                if (Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, _layout.RuntimeDirectory);
                }

                return new UpdateApplyResult(UpdateApplyStatus.Failed, "Не удалось заменить runtime, выполнен rollback: " + ex.Message);
            }
            finally
            {
                DeleteDirectoryIfExists(preserveDirectory);
            }
        }
        catch (Exception ex)
        {
            return new UpdateApplyResult(UpdateApplyStatus.Failed, ex.Message);
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static string? FindValidPackageRoot(string extractDirectory)
    {
        if (RuntimeValidator.Validate(extractDirectory).IsComplete)
        {
            return extractDirectory;
        }

        return Directory.GetDirectories(extractDirectory)
            .FirstOrDefault(directory => RuntimeValidator.Validate(directory).IsComplete);
    }

    private void PreserveUserFiles(string preserveDirectory)
    {
        foreach (var relativePath in PreservedRelativePaths)
        {
            var source = CombineRelative(_layout.RuntimeDirectory, relativePath);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = CombineRelative(preserveDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private void RestoreUserFiles(string preserveDirectory)
    {
        foreach (var relativePath in Directory.Exists(preserveDirectory)
            ? Directory.GetFiles(preserveDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(preserveDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))
            : Array.Empty<string>())
        {
            var source = CombineRelative(preserveDirectory, relativePath);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = CombineRelative(_layout.RuntimeDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private static string CombineRelative(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDirectory, targetDirectory));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(sourceDirectory, targetDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void TryDeleteDirectoryIfExists(string path)
    {
        try
        {
            DeleteDirectoryIfExists(path);
        }
        catch
        {
            // Очистка temp не должна отменять уже применённое обновление или rollback.
        }
    }
}
