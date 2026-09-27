using System.Diagnostics;
using Microsoft.Win32;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class AppInstallerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("zapret-install-").FullName;
    private readonly string _registryKey = @"Software\ZapretManagerTests\" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_registryKey, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Install_CopiesExecutableCreatesShortcutsAndRegistersUninstall()
    {
        var paths = CreatePaths();
        var source = Path.Combine(_root, "Downloads", "Zapret Manager.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "manager");

        new AppInstaller(paths).Install(source, "0.1.0");

        Assert.Equal("manager", File.ReadAllText(paths.ExecutablePath));
        Assert.True(File.Exists(paths.StartMenuShortcut));
        Assert.True(File.Exists(paths.DesktopShortcut));
        using var key = Registry.CurrentUser.OpenSubKey(paths.UninstallRegistryKey);
        Assert.NotNull(key);
        Assert.Equal("Zapret Manager", key.GetValue("DisplayName"));
        Assert.Equal("0.1.0", key.GetValue("DisplayVersion"));
        Assert.Equal($"\"{paths.ExecutablePath}\" --uninstall", key.GetValue("UninstallString"));
        Assert.Equal(paths.InstallDirectory, key.GetValue("InstallLocation"));
    }

    [Fact]
    public void Install_OverExistingInstallation_ReplacesExecutableAndKeepsData()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.InstallDirectory);
        File.WriteAllText(paths.ExecutablePath, "old");
        File.WriteAllText(Path.Combine(paths.InstallDirectory, "config.json"), "{}");
        var source = Path.Combine(_root, "new.exe");
        File.WriteAllText(source, "new");

        new AppInstaller(paths).Install(source, "0.2.0");

        Assert.Equal("new", File.ReadAllText(paths.ExecutablePath));
        Assert.True(File.Exists(Path.Combine(paths.InstallDirectory, "config.json")));
    }

    [Fact]
    public void RemoveIntegration_DeletesShortcutsAndRegistryEntry()
    {
        var paths = CreatePaths();
        var source = Path.Combine(_root, "source.exe");
        File.WriteAllText(source, "manager");
        var installer = new AppInstaller(paths);
        installer.Install(source, "0.1.0");

        installer.RemoveIntegration();

        Assert.False(File.Exists(paths.StartMenuShortcut));
        Assert.False(File.Exists(paths.DesktopShortcut));
        Assert.False(installer.IsRegistered());
    }

    [Fact]
    public void ScheduleDirectoryDeletion_RemovesInstallDirectoryAfterProcessExits()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(Path.Combine(paths.InstallDirectory, "runtime"));
        File.WriteAllText(paths.ExecutablePath, "manager");
        using var finished = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true })!;
        finished.WaitForExit();

        new AppInstaller(paths).ScheduleDirectoryDeletion(finished.Id);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (Directory.Exists(paths.InstallDirectory) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Assert.False(Directory.Exists(paths.InstallDirectory));
    }

    [Fact]
    public void IsInstalledCopy_ComparesContainingDirectory()
    {
        var paths = CreatePaths();

        Assert.True(paths.IsInstalledCopy(paths.ExecutablePath));
        Assert.True(paths.IsInstalledCopy(paths.ExecutablePath.ToUpperInvariant()));
        Assert.False(paths.IsInstalledCopy(Path.Combine(_root, "Downloads", "Zapret Manager.exe")));
    }

    private InstallPaths CreatePaths()
    {
        return new InstallPaths(
            Path.Combine(_root, "Programs", "Zapret Manager"),
            Path.Combine(_root, "StartMenu", "Zapret Manager.lnk"),
            Path.Combine(_root, "Desktop", "Zapret Manager.lnk"),
            _registryKey);
    }
}
