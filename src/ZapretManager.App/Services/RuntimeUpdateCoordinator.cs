namespace ZapretManager.App.Services;

public sealed record RuntimeUpdateOutcome(bool IsSuccess, string Message);

/// <summary>
/// Устанавливает уже скачанное обновление runtime так, чтобы zapret не терял состояние:
/// работающий управляемый winws останавливается перед заменой файлов и запускается снова после неё —
/// даже если замена завершилась ошибкой (тогда работает старый runtime после rollback).
/// </summary>
public sealed class RuntimeUpdateCoordinator
{
    private readonly Func<bool> _isManagedProcessRunning;
    private readonly Func<bool> _hasRuntimeProcesses;
    private readonly Func<ZapretActionKind, Task<ZapretActionResponse>> _executeActionAsync;
    private readonly Func<UpdateApplyResult> _applyUpdate;

    public RuntimeUpdateCoordinator(
        Func<bool> isManagedProcessRunning,
        Func<bool> hasRuntimeProcesses,
        Func<ZapretActionKind, Task<ZapretActionResponse>> executeActionAsync,
        Func<UpdateApplyResult> applyUpdate)
    {
        _isManagedProcessRunning = isManagedProcessRunning;
        _hasRuntimeProcesses = hasRuntimeProcesses;
        _executeActionAsync = executeActionAsync;
        _applyUpdate = applyUpdate;
    }

    public async Task<RuntimeUpdateOutcome> ApplyAsync()
    {
        var wasStartedByManager = _isManagedProcessRunning();
        if (!wasStartedByManager && _hasRuntimeProcesses())
        {
            return new RuntimeUpdateOutcome(
                false,
                "Обновление остановлено: текущий runtime использует внешний winws.exe. Закройте его через его собственный launcher или вручную, затем повторите обновление.");
        }

        if (wasStartedByManager)
        {
            var stopResult = await _executeActionAsync(ZapretActionKind.Stop);
            if (!stopResult.IsSuccess)
            {
                return new RuntimeUpdateOutcome(
                    false,
                    "Не удалось остановить zapret перед обновлением.\n\n" + stopResult.Message);
            }
        }

        UpdateApplyResult applyResult;
        ZapretActionResponse? restartResult = null;
        try
        {
            // Замена runtime — копирование файлов; не держим на нём UI-поток.
            applyResult = await Task.Run(_applyUpdate);
        }
        finally
        {
            if (wasStartedByManager)
            {
                restartResult = await _executeActionAsync(ZapretActionKind.Start);
            }
        }

        var isSuccess = applyResult.IsSuccess && (restartResult is null || restartResult.IsSuccess);
        if (restartResult is null)
        {
            return new RuntimeUpdateOutcome(isSuccess, applyResult.Message);
        }

        var restartMessage = isSuccess
            ? "\nПерезапуск zapret: " + restartResult.Message
            : "\n\nВосстановление состояния zapret: " + restartResult.Message;
        return new RuntimeUpdateOutcome(isSuccess, applyResult.Message + restartMessage);
    }
}
