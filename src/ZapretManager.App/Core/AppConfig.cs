namespace ZapretManager.App.Core;

public sealed class AppConfig
{
    public string? SelectedStrategy { get; set; }

    public string? LastKnownVersion { get; set; }

    public ManagedProcessState? ManagedProcess { get; set; }

    public LastStrategyScanResult? LastStrategyScan { get; set; }

    public bool CheckForUpdatesOnStartup { get; set; }

    public bool StartWithWindows { get; set; }

    /// <summary>Подсказка «менеджер остался в трее» показывается один раз — при первом закрытии окна.</summary>
    public bool TrayHintShown { get; set; }
}
