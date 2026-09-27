using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ZapretManager.App.Infrastructure;

public sealed class CommandRunner : ICommandRunner
{
    /// <summary>
    /// Консольные утилиты Windows (schtasks, sc) пишут в перенаправленный вывод в OEM-кодировке
    /// (на русской системе — cp866), а не в UTF-8. Без неё сообщения превращаются в кракозябры,
    /// а разбор ответа («задание не найдено», путь в XML задания) перестаёт работать.
    /// </summary>
    internal static readonly Encoding ConsoleOutputEncoding = CreateConsoleOutputEncoding();

    public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = ConsoleOutputEncoding,
            StandardErrorEncoding = ConsoleOutputEncoding
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        try
        {
            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            var completed = process.WaitForExit((int)timeout.TotalMilliseconds);
            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
                catch
                {
                    // Ignore kill failures; caller still gets timeout result.
                }

                return new CommandResult(-1, string.Empty, "Command timed out.");
            }

            return new CommandResult(
                process.ExitCode,
                standardOutputTask.GetAwaiter().GetResult(),
                standardErrorTask.GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    public CommandResult StartDetached(string fileName, string arguments, string? workingDirectory = null, bool createNoWindow = true)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            process.Start();
            return new CommandResult(0, string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    private static Encoding CreateConsoleOutputEncoding()
    {
        // В .NET кодовые страницы вроде 866 доступны только через этот провайдер.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.Default;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
