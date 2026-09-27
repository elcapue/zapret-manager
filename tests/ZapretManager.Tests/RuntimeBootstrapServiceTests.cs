using System.Windows.Forms;
using ZapretManager.App.Core;
using ZapretManager.App.Services;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class RuntimeBootstrapServiceTests
{
    [Fact]
    public void BootstrapForm_OffersOnlyDownloadAndExitBeforeInstallation()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-bootstrap-form-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        var configService = new ConfigService(Path.Combine(baseDir, "config.json"));
        var config = configService.LoadOrCreate();
        using var form = new RuntimeBootstrapForm(
            new RuntimeBootstrapService(layout),
            configService,
            config);

        var buttons = form.Controls.OfType<Button>().Select(button => button.Text).ToArray();
        var message = form.Controls.OfType<Label>()
            .Single(label => label.Name == "RuntimeBootstrapMessage");

        Assert.Equal("Zapret Manager", form.Text);
        Assert.Equal(new System.Drawing.Size(520, 220), form.ClientSize);
        Assert.Equal(FormStartPosition.CenterScreen, form.StartPosition);
        Assert.Equal(FormBorderStyle.FixedDialog, form.FormBorderStyle);
        Assert.True(form.ShowInTaskbar);
        Assert.NotNull(form.Icon);
        Assert.Contains(
            form.Controls.OfType<Label>(),
            label => label.Text == "Установка Runtime");
        Assert.Equal(new[] { "Скачать", "Выйти" }, buttons);
        Assert.All(form.Controls.OfType<Button>(), button => Assert.Equal(24, form.ClientSize.Height - button.Bottom));
        Assert.Equal(24, form.ClientSize.Width - form.Controls.OfType<Button>().Max(button => button.Right));
        Assert.Equal(
            "Zapret не найден. Необходимо скачать последнюю версию Flowseal zapret-discord-youtube.",
            message.Text);
    }

    [Fact]
    public void ShouldOfferBootstrap_WhenRuntimeIncomplete_ReturnsTrue()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-bootstrap-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        var service = new RuntimeBootstrapService(layout);

        Assert.True(service.ShouldOfferBootstrap());
    }

    [Fact]
    public void ShouldOfferBootstrap_WhenRuntimeComplete_ReturnsFalse()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-bootstrap-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        Directory.CreateDirectory(Path.Combine(layout.RuntimeDirectory, "bin"));
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "winws.exe"), "x");
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "WinDivert64.sys"), "x");
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "bin", "WinDivert.dll"), "x");
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "service.bat"), "x");
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "general.bat"), "x");
        var service = new RuntimeBootstrapService(layout);

        Assert.False(service.ShouldOfferBootstrap());
    }
}
