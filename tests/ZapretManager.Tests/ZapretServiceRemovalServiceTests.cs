using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ZapretServiceRemovalServiceTests
{
    [Fact]
    public void Remove_WhenZapretServiceDoesNotExist_DoesNotRunMutatingCommands()
    {
        var runner = new FakeCommandRunner();
        var service = new ZapretServiceRemovalService(runner);
        var status = new ZapretStatus { ZapretServiceExists = false };

        var result = service.RemoveForStrategyTests(status);

        Assert.Equal(ZapretServiceRemovalStatus.NotInstalled, result.Status);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void Remove_WhenZapretServiceExists_StopsAndDeletesOnlyZapretService()
    {
        var runner = new FakeCommandRunner();
        runner.Set("net.exe", "stop zapret", new CommandResult(0, string.Empty, string.Empty));
        runner.Set("sc.exe", "delete zapret", new CommandResult(0, string.Empty, string.Empty));
        var service = new ZapretServiceRemovalService(runner);
        var status = new ZapretStatus
        {
            ZapretServiceExists = true,
            ZapretServiceRunning = true,
            WinwsProcessRunning = true
        };

        var result = service.RemoveForStrategyTests(status);

        Assert.Equal(ZapretServiceRemovalStatus.Removed, result.Status);
        Assert.Equal(
            new[]
            {
                ("net.exe", "stop zapret"),
                ("sc.exe", "delete zapret")
            },
            runner.Calls.Select(call => (call.FileName, call.Arguments)).ToArray());
    }

    [Fact]
    public void Remove_WhenDeleteZapretFails_ReturnsCommandFailed()
    {
        var runner = new FakeCommandRunner();
        runner.Set("net.exe", "stop zapret", new CommandResult(0, string.Empty, string.Empty));
        runner.Set("sc.exe", "delete zapret", new CommandResult(1, string.Empty, "delete failed"));
        var service = new ZapretServiceRemovalService(runner);
        var status = new ZapretStatus { ZapretServiceExists = true, ZapretServiceRunning = true };

        var result = service.RemoveForStrategyTests(status);

        Assert.Equal(ZapretServiceRemovalStatus.CommandFailed, result.Status);
        Assert.Contains("delete failed", result.Message);
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        private readonly Dictionary<string, CommandResult> _results = new(StringComparer.OrdinalIgnoreCase);

        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public void Set(string fileName, string arguments, CommandResult result)
        {
            _results[$"{fileName} {arguments}"] = result;
        }

        public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
        {
            Calls.Add((fileName, arguments));
            return _results.TryGetValue($"{fileName} {arguments}", out var result)
                ? result
                : new CommandResult(0, string.Empty, string.Empty);
        }
    }
}
