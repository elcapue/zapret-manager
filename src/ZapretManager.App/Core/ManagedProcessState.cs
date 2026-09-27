namespace ZapretManager.App.Core;

public sealed class ManagedProcessState
{
    public int ProcessId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string StrategyFileName { get; set; } = string.Empty;
}
