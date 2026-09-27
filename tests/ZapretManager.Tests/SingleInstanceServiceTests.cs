using System.Security.Principal;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public async Task TryAcquirePrimary_ElevationHandoffWaitsForPredecessorToReleaseMutex()
    {
        var baseDirectory = Directory.CreateTempSubdirectory("zapret-single-instance-").FullName;
        using var predecessor = new SingleInstanceService(baseDirectory);
        Assert.True(predecessor.TryAcquirePrimary());

        var successor = Task.Run(() =>
        {
            using var service = new SingleInstanceService(baseDirectory);
            return service.TryAcquirePrimary(TimeSpan.FromSeconds(2));
        });

        Thread.Sleep(50);
        Assert.False(successor.IsCompleted);
        predecessor.ReleasePrimary();

        Assert.True(await successor);
    }

    [Fact]
    public void RequestPrimaryExit_InvokesExitHandlerOfRunningInstance()
    {
        var baseDirectory = Directory.CreateTempSubdirectory("zapret-single-instance-").FullName;
        var exitRequested = false;
        var showRequested = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new System.Windows.Forms.Form();
                _ = form.Handle;
                using var primary = new SingleInstanceService(baseDirectory);
                Assert.True(primary.TryAcquirePrimary());
                primary.StartActivationListener(form, () => showRequested = true, () => exitRequested = true);

                // Мьютекс повторно захватывается тем же потоком, поэтому «другой экземпляр» — в другом потоке.
                var clientAcquired = Task.Run(() =>
                {
                    using var client = new SingleInstanceService(baseDirectory + Path.DirectorySeparatorChar);
                    var acquired = client.TryAcquirePrimary(notifyPrimary: false);
                    client.RequestPrimaryExit();
                    return acquired;
                });

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!exitRequested && DateTime.UtcNow < deadline)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }

                Assert.False(clientAcquired.Result);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));

        Assert.Null(failure);
        Assert.True(exitRequested);
        Assert.False(showRequested);
    }

    [Fact]
    public void CreatePipeSecurity_AllowsCurrentUserAndUsesMediumIntegrityLabel()
    {
        var currentUser = WindowsIdentity.GetCurrent().User;

        var descriptor = SingleInstanceService.CreatePipeSecurityDescriptorSddl();

        Assert.NotNull(currentUser);
        Assert.Contains(currentUser.Value, descriptor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S:(ML;;NW;;;ME)", descriptor, StringComparison.OrdinalIgnoreCase);
    }
}
