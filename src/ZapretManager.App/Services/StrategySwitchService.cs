using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed record StrategySwitchResult(
    bool IsSuccess,
    bool PreviousStrategyRestored,
    ZapretActionResponse SwitchResponse,
    ZapretActionResponse? RestoreResponse = null);

public sealed class StrategySwitchService
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Func<ZapretActionKind, Task<ZapretActionResponse>> _executeActionAsync;
    private readonly Func<bool> _isManagedProcessRunning;

    public StrategySwitchService(
        ConfigService configService,
        AppConfig config,
        Func<ZapretActionKind, Task<ZapretActionResponse>> executeActionAsync,
        Func<bool> isManagedProcessRunning)
    {
        _configService = configService;
        _config = config;
        _executeActionAsync = executeActionAsync;
        _isManagedProcessRunning = isManagedProcessRunning;
    }

    public async Task<StrategySwitchResult> SwitchAsync(StrategyInfo strategy)
    {
        var previousStrategy = _config.SelectedStrategy;
        try
        {
            StrategySelectionService.SelectStrategy(_configService, _config, strategy);

            var switchResponse = await _executeActionAsync(ZapretActionKind.Restart);
            if (switchResponse.IsSuccess)
            {
                return new StrategySwitchResult(true, false, switchResponse);
            }

            _config.SelectedStrategy = previousStrategy;
            _configService.Save(_config);
            var restoreResponse = await _executeActionAsync(ZapretActionKind.Start);
            var restored = restoreResponse.IsSuccess || _isManagedProcessRunning();
            return new StrategySwitchResult(false, restored, switchResponse, restoreResponse);
        }
        catch
        {
            _config.SelectedStrategy = previousStrategy;
            _configService.Save(_config);
            throw;
        }
    }
}
