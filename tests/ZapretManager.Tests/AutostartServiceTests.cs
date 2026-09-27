using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class AutostartServiceTests
{
    private static readonly ScheduledTaskLookup Missing = new(ScheduledTaskLookupStatus.Missing);

    [Fact]
    public void BuildCreateArguments_UsesCurrentExecutableBackgroundModeAndUniqueName()
    {
        var service = new AutostartService(new RecordingCommandRunner(), @"C:\Тестовая папка\Zapret Manager.exe", new FakeTaskReader());

        var arguments = service.BuildCreateArguments();

        Assert.Contains("/SC ONLOGON", arguments);
        Assert.Contains("/DELAY 0000:05", arguments);
        Assert.Contains("/RL HIGHEST", arguments);
        Assert.Contains("/IT", arguments);
        Assert.Contains("Zapret Manager.exe", arguments);
        Assert.Contains("--autostart", arguments);
        Assert.Contains("Тестовая папка", arguments);
        Assert.Contains($"/TN \"{service.TaskName}\"", arguments);
        Assert.NotEqual("Zapret Manager", service.TaskName);
    }

    [Fact]
    public void BuildTaskName_DiffersForIndependentCopies()
    {
        var first = AutostartService.BuildTaskName(@"C:\Apps\One\Zapret Manager.exe");
        var second = AutostartService.BuildTaskName(@"C:\Apps\Two\Zapret Manager.exe");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SetEnabled_WhenTaskIsMissing_CreatesInteractiveHighestPrivilegeTask()
    {
        var runner = new RecordingCommandRunner(new CommandResult(0, "SUCCESS", string.Empty));
        var reader = new FakeTaskReader();
        var service = new AutostartService(runner, @"C:\Apps\Zapret Manager.exe", reader);

        var result = service.SetEnabled(true);

        Assert.True(result.IsSuccess);
        var create = Assert.Single(runner.Calls);
        Assert.Contains("/Create", create.Arguments);
        Assert.Contains($"/TN \"{service.TaskName}\"", create.Arguments);
        Assert.Equal([service.TaskName], reader.Reads);
    }

    [Fact]
    public void SetEnabled_WhenDerivedTaskIsForeign_DoesNotOverwriteIt()
    {
        const string executablePath = @"C:\Apps\Zapret Manager.exe";
        var runner = new RecordingCommandRunner();
        var taskName = AutostartService.BuildTaskName(executablePath);
        var reader = new FakeTaskReader { [taskName] = Exists(@"C:\Other\Other.exe", "--autostart") };
        var service = new AutostartService(runner, executablePath, reader);

        var result = service.SetEnabled(true);

        Assert.False(result.IsSuccess);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void SetEnabled_WhenQueryFails_ReportsReasonAndDoesNotCreate()
    {
        var runner = new RecordingCommandRunner();
        var reader = new FakeTaskReader { Default = new ScheduledTaskLookup(ScheduledTaskLookupStatus.Failed, Error: "Access denied") };
        var service = new AutostartService(runner, @"C:\Apps\Zapret Manager.exe", reader);

        var result = service.SetEnabled(true);

        Assert.False(result.IsSuccess);
        Assert.Equal("Не удалось проверить задание автозапуска. Access denied", result.Message);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void SetDisabled_WhenOwnedTaskExists_DeletesItAfterXmlVerification()
    {
        const string executablePath = @"C:\Apps\Zapret Manager.exe";
        var runner = new RecordingCommandRunner(new CommandResult(0, "deleted", string.Empty));
        var taskName = AutostartService.BuildTaskName(executablePath);
        var reader = new FakeTaskReader { [taskName] = Exists(executablePath, "--autostart") };
        var service = new AutostartService(runner, executablePath, reader);

        var result = service.SetEnabled(false);

        Assert.True(result.IsSuccess);
        Assert.Equal($"/Delete /TN \"{service.TaskName}\" /F", Assert.Single(runner.Calls).Arguments);
    }

    [Fact]
    public void SetDisabled_WhenTaskPathDoesNotMatch_DoesNotDeleteIt()
    {
        const string executablePath = @"C:\Apps\Zapret Manager.exe";
        var runner = new RecordingCommandRunner();
        var taskName = AutostartService.BuildTaskName(executablePath);
        var reader = new FakeTaskReader { [taskName] = Exists(@"C:\Other\Zapret Manager.exe", "--autostart") };
        var service = new AutostartService(runner, executablePath, reader);

        var result = service.SetEnabled(false);

        Assert.False(result.IsSuccess);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void SetDisabled_WhenTaskQueryFails_DoesNotSilentlyForgetConfiguredTask()
    {
        var reader = new FakeTaskReader { Default = new ScheduledTaskLookup(ScheduledTaskLookupStatus.Failed, Error: "Access denied") };
        var service = new AutostartService(new RecordingCommandRunner(), @"C:\Apps\Zapret Manager.exe", reader);

        var result = service.SetEnabled(false);

        Assert.False(result.IsSuccess);
        Assert.Contains("Access denied", result.Message);
    }

    [Fact]
    public void SetDisabled_WhenTaskDoesNotExist_IsSuccessfulNoOp()
    {
        var runner = new RecordingCommandRunner();
        var reader = new FakeTaskReader();
        var service = new AutostartService(runner, @"C:\Apps\Zapret Manager.exe", reader);

        var result = service.SetEnabled(false);

        Assert.True(result.IsSuccess);
        Assert.Empty(runner.Calls);
        Assert.Equal([service.TaskName], reader.Reads);
    }

    [Fact]
    public void ScheduledTaskReader_ReportsMissingTaskByErrorCodeRegardlessOfLanguage()
    {
        var lookup = new ScheduledTaskReader().Read($"ZapretManager-test-{Guid.NewGuid():N}");

        Assert.Equal(ScheduledTaskLookupStatus.Missing, lookup.Status);
    }

    private static ScheduledTaskLookup Exists(string command, string arguments)
    {
        return new ScheduledTaskLookup(
            ScheduledTaskLookupStatus.Exists,
            $"<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Actions><Exec><Command>{command}</Command><Arguments>{arguments}</Arguments></Exec></Actions></Task>");
    }

    private sealed class FakeTaskReader : IScheduledTaskReader
    {
        private readonly Dictionary<string, ScheduledTaskLookup> _tasks = new(StringComparer.OrdinalIgnoreCase);

        public ScheduledTaskLookup Default { get; init; } = Missing;

        public List<string> Reads { get; } = [];

        public ScheduledTaskLookup this[string taskName]
        {
            set => _tasks[taskName] = value;
        }

        public ScheduledTaskLookup Read(string taskName)
        {
            Reads.Add(taskName);
            return _tasks.TryGetValue(taskName, out var lookup) ? lookup : Default;
        }
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        private readonly Queue<CommandResult> _results;

        public RecordingCommandRunner(params CommandResult[] results)
        {
            _results = new Queue<CommandResult>(results);
        }

        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
        {
            Calls.Add((fileName, arguments));
            return _results.Count > 0
                ? _results.Dequeue()
                : new CommandResult(0, string.Empty, string.Empty);
        }

        public CommandResult StartDetached(
            string fileName,
            string arguments,
            string? workingDirectory = null,
            bool createNoWindow = true)
        {
            throw new NotSupportedException();
        }
    }
}
