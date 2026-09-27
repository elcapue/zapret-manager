using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed class RuntimeBootstrapForm : Form
{
    private readonly RuntimeBootstrapService _bootstrapService;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Label _messageLabel;
    private readonly Button _downloadButton;
    private readonly Button _exitButton;
    private CancellationTokenSource? _downloadCancellation;

    public RuntimeBootstrapForm(
        RuntimeBootstrapService bootstrapService,
        ConfigService configService,
        AppConfig config,
        bool startDownloadImmediately = false)
    {
        _bootstrapService = bootstrapService;
        _configService = configService;
        _config = config;

        Text = "Zapret Manager";
        ClientSize = new Size(UiTheme.DialogWidth, UiTheme.DialogHeight);
        UiTheme.ApplyDialogWindow(this, FormStartPosition.CenterScreen, showInTaskbar: true);

        var titleLabel = new Label
        {
            Text = "Установка Runtime",
            Location = new Point(24, 22),
            Size = new Size(472, 30)
        };
        UiTheme.StyleLabel(titleLabel, UiTheme.Text, 12F, FontStyle.Bold);

        _messageLabel = new Label
        {
            Name = "RuntimeBootstrapMessage",
            Text = "Zapret не найден. Необходимо скачать последнюю версию Flowseal zapret-discord-youtube.",
            Location = new Point(24, 68),
            Size = new Size(472, 58)
        };
        UiTheme.StyleLabel(_messageLabel, UiTheme.MutedText, 9F);

        _downloadButton = new ThemedButton
        {
            Name = "RuntimeDownloadButton",
            Text = "Скачать",
            Location = new Point(294, 162),
            Size = new Size(100, UiTheme.DialogButtonHeight),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleButton(_downloadButton, UiButtonKind.Primary);
        _downloadButton.Click += async (_, _) => await DownloadAsync();

        _exitButton = new ThemedButton
        {
            Name = "RuntimeExitButton",
            Text = "Выйти",
            Location = new Point(404, 162),
            Size = new Size(92, UiTheme.DialogButtonHeight),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        UiTheme.StyleButton(_exitButton, UiButtonKind.Secondary);
        _exitButton.Click += (_, _) =>
        {
            _downloadCancellation?.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _downloadButton;
        CancelButton = _exitButton;
        FormClosing += (_, _) => _downloadCancellation?.Cancel();
        if (startDownloadImmediately)
        {
            // Сразу после установки второй клик не нужен: пользователь уже нажал «Установить».
            Shown += async (_, _) => await DownloadAsync();
        }

        Controls.Add(titleLabel);
        Controls.Add(_messageLabel);
        Controls.Add(_downloadButton);
        Controls.Add(_exitButton);
    }

    private async Task DownloadAsync()
    {
        if (_downloadCancellation is not null)
        {
            return;
        }

        _downloadCancellation = new CancellationTokenSource();
        _downloadButton.Enabled = false;
        _downloadButton.Text = "Скачивание...";
        _messageLabel.Text = "Скачиваем и устанавливаем последнюю версию runtime...";

        try
        {
            using var httpClient = new HttpClient();
            var result = await _bootstrapService.InstallLatestAsync(httpClient, _config, _downloadCancellation.Token);
            if (!result.IsSuccess || _bootstrapService.ShouldOfferBootstrap())
            {
                var reason = result.IsSuccess
                    ? "Установка завершилась, но обязательные файлы runtime не найдены."
                    : result.Message;
                ShowRetry(reason);
                return;
            }

            _configService.Save(_config);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                ShowRetry("Загрузка отменена.");
            }
        }
        catch (Exception ex)
        {
            ShowRetry("Не удалось скачать runtime. " + ex.Message);
        }
        finally
        {
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
        }
    }

    private void ShowRetry(string reason)
    {
        _messageLabel.Text = reason + "\nМожно повторить загрузку или выйти.";
        _downloadButton.Text = "Повторить";
        _downloadButton.Enabled = true;
    }
}
