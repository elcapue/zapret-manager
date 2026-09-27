using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace ZapretManager.App.Services;

/// <summary>
/// Гарантирует единственный экземпляр приложения. Первый экземпляр держит
/// именованный мьютекс и слушает named pipe; повторный запуск отправляет
/// сигнал через pipe и завершается, а первый экземпляр показывает окно.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    internal const string ElevationHandoffArgument = "--elevation-handoff";
    private const string ShowWindowSignal = "show-window";
    // Просьба завершиться штатно (с остановкой своего zapret) — ею пользуется удаление программы.
    // Отправить сигнал может только тот же пользователь или администратор (ACL pipe), а штатный выход
    // ничего не делает сверх того, что пользователь может сделать сам через «Выход».
    private const string ExitSignal = "exit";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;

    public SingleInstanceService(string baseDirectory)
    {
        // Имена привязаны к папке приложения: копии в разных
        // каталогах считаются независимыми экземплярами.
        var normalizedDirectory = Path.GetFullPath(baseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var key = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalizedDirectory)));
        _mutexName = $@"Local\ZapretManager.SingleInstance.{key}";
        _pipeName = $"ZapretManager.SingleInstance.{key}";
    }

    /// <summary>
    /// Пытается занять роль первичного экземпляра.
    /// true — этот процесс первичный и должен продолжить запуск.
    /// false — уже есть запущенный экземпляр; если <paramref name="notifyPrimary"/>, ему отправлен сигнал показать окно.
    /// </summary>
    public bool TryAcquirePrimary(TimeSpan? handoffWait = null, bool notifyPrimary = true)
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: false, _mutexName);
            try
            {
                _ownsMutex = _mutex.WaitOne(handoffWait ?? TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }

            if (_ownsMutex)
            {
                return true;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Мьютекс мог быть создан напрямую elevated-процессом. В этом
            // случае medium-клиент всё равно может активировать его через pipe.
        }

        DisposeMutex();
        if (notifyPrimary)
        {
            NotifyPrimaryInstance(ShowWindowSignal);
        }

        return false;
    }

    /// <summary>Просит уже запущенный экземпляр завершиться штатно. Ничего не делает, если его нет.</summary>
    public void RequestPrimaryExit()
    {
        NotifyPrimaryInstance(ExitSignal);
    }

    /// <summary>
    /// Освобождает первичную роль для elevated-процесса после успешного runas.
    /// Elevated-процесс ждёт этот mutex и поэтому не проигрывает гонку handoff.
    /// </summary>
    public void ReleasePrimary()
    {
        DisposeMutex();
    }

    /// <summary>
    /// Запускает прослушивание сигналов от повторных запусков.
    /// Вызывать только у первичного экземпляра. Колбэк всегда приходит
    /// в UI-потоке через Control.Invoke.
    /// </summary>
    public void StartActivationListener(Control uiInvoker, Action onShowWindowRequested, Action? onExitRequested = null)
    {
        if (_mutex is null)
        {
            throw new InvalidOperationException("TryAcquirePrimary() must succeed before starting the listener.");
        }

        _listenerCancellation = new CancellationTokenSource();
        var token = _listenerCancellation.Token;
        _listenerTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = CreateActivationPipe();

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server);
                    var signal = await reader.ReadLineAsync(token);
                    var handler = signal switch
                    {
                        ShowWindowSignal => onShowWindowRequested,
                        ExitSignal => onExitRequested,
                        _ => null
                    };
                    if (handler is null)
                    {
                        continue;
                    }

                    if (uiInvoker.IsDisposed)
                    {
                        return;
                    }

                    uiInvoker.BeginInvoke(handler);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // Клиент отключился раньше времени — просто ждём следующий сигнал.
                }
                catch (UnauthorizedAccessException)
                {
                    // Повторим создание listener: transient ACL-ошибка не должна
                    // завершать фоновую задачу активации.
                }
            }
        }, token);
    }

    internal static string CreatePipeSecurityDescriptorSddl()
    {
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Не удалось определить SID текущего пользователя.");
        return $"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGW;;;{currentUser.Value})S:(ML;;NW;;;ME)";
    }

    private NamedPipeServerStream CreateActivationPipe()
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                CreatePipeSecurityDescriptorSddl(),
                SddlRevision,
                out var securityDescriptor,
                out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var securityAttributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = securityDescriptor
            };
            var handle = CreateNamedPipeW(
                @"\\.\pipe\" + _pipeName,
                PipeAccessInbound | FileFlagOverlapped,
                PipeTypeByte | PipeReadModeByte | PipeWait | PipeRejectRemoteClients,
                maxInstances: 1,
                outBufferSize: 0,
                inBufferSize: 0,
                defaultTimeout: 0,
                ref securityAttributes);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }

            return new NamedPipeServerStream(
                PipeDirection.In,
                isAsync: true,
                isConnected: false,
                handle);
        }
        finally
        {
            LocalFree(securityDescriptor);
        }
    }

    private void NotifyPrimaryInstance(string signal)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.WriteLine(signal);
            writer.Flush();
        }
        catch (TimeoutException)
        {
            // Первичный экземпляр занят или завершается — второй процесс всё равно выходит.
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
            // Вторичный запуск не должен падать, даже если primary завершается
            // или объект создан со старыми правами доступа.
        }
    }

    private void DisposeMutex()
    {
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownsMutex = false;
        }

        _mutex?.Dispose();
        _mutex = null;
    }

    public void Dispose()
    {
        _listenerCancellation?.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _listenerCancellation?.Dispose();
        DisposeMutex();
    }

    private const uint SddlRevision = 1;
    private const uint PipeAccessInbound = 0x00000001;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint PipeTypeByte = 0x00000000;
    private const uint PipeReadModeByte = 0x00000000;
    private const uint PipeWait = 0x00000000;
    private const uint PipeRejectRemoteClients = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipeW(
        string name,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint outBufferSize,
        uint inBufferSize,
        uint defaultTimeout,
        ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
