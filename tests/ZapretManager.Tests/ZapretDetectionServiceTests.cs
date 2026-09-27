using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ZapretDetectionServiceTests
{
    [Fact]
    public void Detect_WhenNothingIsInstalled_ReportsStopped()
    {
        var service = new ZapretDetectionService(new FakeServiceStateReader(), isWinwsRunning: () => false);

        var status = service.Detect();

        Assert.False(status.ZapretServiceExists);
        Assert.False(status.ZapretServiceRunning);
        Assert.False(status.WinwsProcessRunning);
    }

    [Fact]
    public void Detect_WhenZapretServiceAndWinwsAreRunning_ReturnsRunningStatus()
    {
        var services = new FakeServiceStateReader { ["zapret"] = ServiceState.Running };
        var service = new ZapretDetectionService(services, isWinwsRunning: () => true);

        var status = service.Detect();

        Assert.True(status.ZapretServiceExists);
        Assert.True(status.ZapretServiceRunning);
        Assert.True(status.WinwsProcessRunning);
    }

    [Fact]
    public void WindowsServiceStateReader_WhenServiceDoesNotExist_ReturnsMissing()
    {
        var reader = new WindowsServiceStateReader();

        Assert.Equal(ServiceState.Missing, reader.Query("ZapretManager-Test-" + Guid.NewGuid().ToString("N")));
    }

    private sealed class FakeServiceStateReader : IServiceStateReader
    {
        private readonly Dictionary<string, ServiceState> _states = new(StringComparer.OrdinalIgnoreCase);

        public ServiceState this[string name]
        {
            set => _states[name] = value;
        }

        public ServiceState Query(string serviceName) => _states.GetValueOrDefault(serviceName, ServiceState.Missing);
    }
}
