using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

/// <summary>
/// Первый экран скачанного exe: одна кнопка ставит менеджер в папку пользователя,
/// создаёт ярлыки и регистрирует программу в «Приложениях».
/// </summary>
public sealed class InstallForm : Form
{
    private readonly Label _messageLabel;
    private readonly Button _installButton;

    public InstallForm(InstallPaths paths, Action install, string? installedVersion)
    {
        var isUpdate = installedVersion is not null;
        Text = "Zapret Manager";
        ClientSize = new Size(UiTheme.DialogWidth, UiTheme.DialogHeight);
        UiTheme.ApplyDialogWindow(this, FormStartPosition.CenterScreen, showInTaskbar: true);

        var titleLabel = new Label
        {
            Text = isUpdate ? "Обновление Zapret Manager" : "Установка Zapret Manager",
            Location = new Point(24, 22),
            Size = new Size(472, 30)
        };
        UiTheme.StyleLabel(titleLabel, UiTheme.Text, 12F, FontStyle.Bold);

        _messageLabel = new Label
        {
            Name = "InstallMessage",
            Text = isUpdate
                ? $"Установлена версия {installedVersion}, этот файл — версия {ManagerUpdateService.CurrentVersion}. " +
                  "Настройки и скачанный zapret сохранятся."
                : $"Программа установится в папку\n{paths.InstallDirectory}\n" +
                  "Ярлыки появятся в меню «Пуск» и на рабочем столе.\n" +
                  "Удалить можно в «Параметры → Приложения».",
            Location = new Point(24, 62),
            Size = new Size(472, 84)
        };
        UiTheme.StyleLabel(_messageLabel, UiTheme.MutedText, 9F);

        _installButton = new ThemedButton
        {
            Name = "InstallButton",
            Text = isUpdate ? "Обновить" : "Установить",
            Location = new Point(284, 162),
            Size = new Size(110, UiTheme.DialogButtonHeight),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleButton(_installButton, UiButtonKind.Primary);
        _installButton.Click += (_, _) => RunInstall(install);

        var exitButton = new ThemedButton
        {
            Name = "InstallExitButton",
            Text = "Выйти",
            Location = new Point(404, 162),
            Size = new Size(92, UiTheme.DialogButtonHeight),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        UiTheme.StyleButton(exitButton, UiButtonKind.Secondary);

        AcceptButton = _installButton;
        CancelButton = exitButton;
        Controls.Add(titleLabel);
        Controls.Add(_messageLabel);
        Controls.Add(_installButton);
        Controls.Add(exitButton);
    }

    private void RunInstall(Action install)
    {
        _installButton.Enabled = false;
        try
        {
            install();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Runtime.InteropServices.COMException)
        {
            _messageLabel.Text = "Не удалось установить Zapret Manager. " + ex.Message + "\nМожно повторить или выйти.";
            _installButton.Text = "Повторить";
            _installButton.Enabled = true;
        }
    }
}
