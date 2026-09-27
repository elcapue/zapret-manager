using ZapretManager.App.Infrastructure;

namespace ZapretManager.Tests;

public sealed class CommandRunnerTests
{
    [Fact]
    public void Run_WhenCommandWritesMoreThanPipeBuffer_CompletesWithoutDeadlock()
    {
        var runner = new CommandRunner();

        var result = runner.Run(
            "cmd.exe",
            "/d /c for /L %i in (1,1,20000) do @echo output",
            TimeSpan.FromSeconds(10));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("output", result.StandardOutput);
    }
}
