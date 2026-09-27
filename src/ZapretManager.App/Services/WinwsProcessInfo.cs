namespace ZapretManager.App.Services;

public sealed record WinwsProcessInfo(int ProcessId, DateTime StartedAtUtc, string ExecutablePath);
