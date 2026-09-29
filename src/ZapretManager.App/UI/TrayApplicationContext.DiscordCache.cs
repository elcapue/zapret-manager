using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    private bool _discordCacheClearing;

    public async void ClearDiscordCache()
    {
        if (_exitInProgress || _discordCacheClearing)
        {
            return;
        }

        var service = new DiscordCacheService();
        var installed = service.FindInstalled();
        if (installed.Count == 0)
        {
            ThemedMessageBox.Show(
                "Discord не найден: нет папок Discord, PTB, Canary или Development в %APPDATA%.",
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirmation = ThemedMessageBox.ShowAction(
            $"Очистить кеш: {string.Join(", ", installed)}?\n\n" +
            "Discord будет закрыт — после очистки запустите его заново. " +
            "Настройки и вход в аккаунт не затрагиваются.",
            "Zapret Manager",
            "Очистить",
            "Отмена",
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        _discordCacheClearing = true;
        try
        {
            var result = await Task.Run(service.Clear);
            _logger.Info("Discord cache clear: " + result.Message.Replace('\n', ' '));
            if (result.IsSuccess)
            {
                _notifications.ShowInformation(result.Message);
                return;
            }

            ThemedMessageBox.Show(result.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Discord cache clear failed.", ex);
            ThemedMessageBox.Show("Не удалось очистить кеш Discord.\n\n" + ex.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _discordCacheClearing = false;
        }
    }
}
