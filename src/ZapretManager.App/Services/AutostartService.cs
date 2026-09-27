using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

public sealed record AutostartUpdateResult(bool IsSuccess, string Message);

public sealed class AutostartService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    private readonly ICommandRunner _commandRunner;
    private readonly IScheduledTaskReader _taskReader;
    private readonly string _executablePath;

    public AutostartService(
        ICommandRunner commandRunner,
        string executablePath,
        IScheduledTaskReader? taskReader = null)
    {
        _commandRunner = commandRunner;
        _taskReader = taskReader ?? new ScheduledTaskReader();
        _executablePath = Path.GetFullPath(executablePath);
        TaskName = BuildTaskName(_executablePath);
    }

    internal string TaskName { get; }

    public AutostartUpdateResult SetEnabled(bool enabled)
    {
        return enabled ? Enable() : Disable();
    }

    public AutostartUpdateResult EnsureEnabled()
    {
        return Enable();
    }

    internal string BuildCreateArguments()
    {
        return $"/Create /TN \"{TaskName}\" /SC ONLOGON /DELAY 0000:05 /RL HIGHEST /IT /F /TR \"\\\"{_executablePath}\\\" --autostart\"";
    }

    internal static string BuildTaskName(string executablePath)
    {
        var normalizedPath = Path.GetFullPath(executablePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
        return $"Zapret Manager {hash[..12]}";
    }

    private AutostartUpdateResult Enable()
    {
        var existing = _taskReader.Read(TaskName);
        if (existing.Status == ScheduledTaskLookupStatus.Failed)
        {
            return new AutostartUpdateResult(false, BuildQueryFailureMessage(existing));
        }

        if (existing.Status == ScheduledTaskLookupStatus.Exists && !IsOwnedTaskXml(existing.Xml))
        {
            return new AutostartUpdateResult(
                false,
                $"Задание «{TaskName}» уже существует и запускает другую программу.");
        }

        var result = _commandRunner.Run("schtasks.exe", BuildCreateArguments(), CommandTimeout);
        if (result.ExitCode != 0)
        {
            return new AutostartUpdateResult(
                false,
                BuildFailureMessage("Не удалось создать задание автозапуска.", result));
        }

        return new AutostartUpdateResult(true, "Автозапуск zapret включён.");
    }

    private AutostartUpdateResult Disable()
    {
        var query = _taskReader.Read(TaskName);
        if (query.Status == ScheduledTaskLookupStatus.Missing)
        {
            return new AutostartUpdateResult(true, "Автозапуск zapret выключен.");
        }

        if (query.Status == ScheduledTaskLookupStatus.Failed)
        {
            return new AutostartUpdateResult(false, BuildQueryFailureMessage(query));
        }

        if (!IsOwnedTaskXml(query.Xml))
        {
            return new AutostartUpdateResult(false, $"Задание «{TaskName}» не принадлежит этой копии.");
        }

        var deletion = _commandRunner.Run(
            "schtasks.exe",
            $"/Delete /TN \"{TaskName}\" /F",
            CommandTimeout);
        return deletion.ExitCode == 0
            ? new AutostartUpdateResult(true, "Автозапуск zapret выключен.")
            : new AutostartUpdateResult(
                false,
                BuildFailureMessage("Не удалось удалить задание автозапуска.", deletion));
    }

    private bool IsOwnedTaskXml(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            var command = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Command")
                ?.Value.Trim().Trim('"');
            var arguments = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Arguments")
                ?.Value;
            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(arguments))
            {
                return false;
            }

            var normalizedCommand = Path.GetFullPath(command);
            var hasAutostartArgument = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(argument => string.Equals(argument.Trim('"'), "--autostart", StringComparison.OrdinalIgnoreCase));
            return hasAutostartArgument &&
                   string.Equals(normalizedCommand, _executablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string BuildQueryFailureMessage(ScheduledTaskLookup lookup)
    {
        const string prefix = "Не удалось проверить задание автозапуска.";
        return string.IsNullOrWhiteSpace(lookup.Error) ? prefix : $"{prefix} {lookup.Error.Trim()}";
    }

    private static string BuildFailureMessage(string prefix, CommandResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        return string.IsNullOrWhiteSpace(details) ? prefix : $"{prefix} {details}";
    }
}
