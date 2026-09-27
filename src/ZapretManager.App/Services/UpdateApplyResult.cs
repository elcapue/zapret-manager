using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public enum UpdateApplyStatus
{
    Applied,
    NoZipAsset,
    HashMismatch,
    InvalidPackage,
    Failed
}

public sealed record UpdateApplyResult(UpdateApplyStatus Status, string Message)
{
    public bool IsSuccess => Status == UpdateApplyStatus.Applied;
}

public sealed record UpdatePrepareResult(
    UpdateApplyStatus Status,
    string Message,
    PreparedUpdate? PreparedUpdate)
{
    public bool IsSuccess => PreparedUpdate is not null;
}

public sealed class PreparedUpdate : IDisposable
{
    internal PreparedUpdate(string workDirectory, string packageRoot, GitHubReleaseInfo release)
    {
        WorkDirectory = workDirectory;
        PackageRoot = packageRoot;
        Release = release;
    }

    internal string WorkDirectory { get; }

    internal string PackageRoot { get; }

    internal GitHubReleaseInfo Release { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkDirectory))
            {
                Directory.Delete(WorkDirectory, recursive: true);
            }
        }
        catch
        {
            // Очистка temp не должна менять результат установки или rollback.
        }
    }
}
