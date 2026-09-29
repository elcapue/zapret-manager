using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace ZapretManager.App.Services;

/// <summary>
/// Проверяет права администратора и при необходимости перезапускает
/// приложение с повышением (один запрос UAC при первом запуске).
/// </summary>
public static class ElevationService
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Перезапускает текущий процесс с правами администратора.
    /// Возвращает true, если перезапуск инициирован (текущий процесс должен завершиться).
    /// false — пользователь отклонил UAC или перезапуск не удался.
    /// </summary>
    public static bool TryRestartElevated(IAppLogger logger, IEnumerable<string>? arguments = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (var argument in arguments ?? [])
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (IsElevationDeclinedByUser(ex))
        {
            logger.Info("Elevation declined by user.");
            return false;
        }
        catch (Exception ex)
        {
            logger.Error("Elevation restart failed.", ex);
            return false;
        }
    }

    /// <summary>Отказ пользователя в диалоге UAC (ERROR_CANCELLED) — единственный ожидаемый отказ runas.</summary>
    internal static bool IsElevationDeclinedByUser(Exception exception)
    {
        const int ErrorCancelled = 1223;
        return exception is Win32Exception { NativeErrorCode: ErrorCancelled };
    }
}
