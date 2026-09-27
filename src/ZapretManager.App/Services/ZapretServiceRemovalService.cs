using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

public sealed class ZapretServiceRemovalService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly ICommandRunner _commandRunner;

    public ZapretServiceRemovalService(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ZapretServiceRemovalResult RemoveForStrategyTests(ZapretStatus status)
    {
        if (!status.ZapretServiceExists)
        {
            return new ZapretServiceRemovalResult(
                ZapretServiceRemovalStatus.NotInstalled,
                "Windows service zapret не установлен.");
        }

        if (status.ZapretServiceRunning)
        {
            var stopZapret = Run("net.exe", "stop zapret");
            if (stopZapret.Status == ZapretServiceRemovalStatus.CommandFailed)
            {
                return stopZapret;
            }
        }

        var deleteZapret = Run("sc.exe", "delete zapret");
        if (deleteZapret.Status == ZapretServiceRemovalStatus.CommandFailed)
        {
            return deleteZapret;
        }

        return new ZapretServiceRemovalResult(
            ZapretServiceRemovalStatus.Removed,
            "Windows service zapret удалён. WinDivert-службы и внешние winws.exe менеджер не изменяет.");
    }

    private ZapretServiceRemovalResult Run(string fileName, string arguments)
    {
        var result = _commandRunner.Run(fileName, arguments, CommandTimeout);
        if (result.ExitCode == 0)
        {
            return new ZapretServiceRemovalResult(ZapretServiceRemovalStatus.Removed, string.Empty);
        }

        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Команда {fileName} {arguments} завершилась с кодом {result.ExitCode}."
            : result.StandardError;
        return new ZapretServiceRemovalResult(ZapretServiceRemovalStatus.CommandFailed, message);
    }
}
