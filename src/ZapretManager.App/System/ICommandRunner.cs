namespace ZapretManager.App.Infrastructure;

public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null);

    // Раньше по умолчанию вызывался Run с нулевым таймаутом, что сразу убивало процесс.
    // Реализации, которым нужен фоновый запуск, обязаны переопределить метод явно.
    CommandResult StartDetached(string fileName, string arguments, string? workingDirectory = null, bool createNoWindow = true)
    {
        throw new NotSupportedException($"{GetType().Name} не поддерживает фоновый запуск процессов.");
    }
}
