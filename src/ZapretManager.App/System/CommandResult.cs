namespace ZapretManager.App.Infrastructure;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
