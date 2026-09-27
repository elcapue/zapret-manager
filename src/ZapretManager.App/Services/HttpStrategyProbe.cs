using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace ZapretManager.App.Services;

/// <summary>
/// Проверяет доступность целей прямыми HTTPS-запросами (без прокси).
/// Каждая попытка состоит из двух шагов, и засчитывается только если прошли оба:
/// 1) рукопожатие «как у браузера» (<see cref="BrowserTlsHandshake"/>) — ловит стратегии,
///    которые пропускают TLS 1.2 от Schannel, но глушат TLS 1.3 от Chrome и Discord;
/// 2) HTTPS-запрос с чтением тела до 64 КБ — ловит обрыв передачи после первых килобайт.
/// Экземпляр живёт один прогон автовыбора: DNS каждой цели резолвится один раз
/// и дальше все стратегии подключаются к одним и тем же IP, поэтому на результат
/// не влияют ни DNS-джиттер, ни DNS-блокировки (их zapret всё равно не лечит).
/// Если DNS провайдера адрес не нашёл, используется DNS-over-HTTPS — как в браузере.
/// </summary>
public sealed class HttpStrategyProbe : IStrategyProbe
{
    /// <summary>
    /// Читаем тело до 64 КБ: так ловится типичная блокировка «обрыв после 16–20 КБ»,
    /// которую HEAD-запрос или одно рукопожатие не видят.
    /// </summary>
    internal const int BodyLimitBytes = 64 * 1024;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(6);

    private readonly ConcurrentDictionary<string, Lazy<Task<ResolvedHost?>>> _dnsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>>? _resolveHostFallback;
    private readonly Func<IPAddress[], string, int, CancellationToken, Task<bool>> _browserHandshake;
    private readonly Func<IPAddress[], SslProtocols, Action, HttpMessageHandler> _createHandler;
    private readonly IReadOnlyList<SslProtocols> _protocols;
    private readonly int _maxParallelRequests;
    private readonly TimeSpan _requestTimeout;

    public HttpStrategyProbe()
        : this(
            null,
            null,
            null,
            null,
            null,
            browserHandshake: (addresses, host, port, token) => BrowserTlsHandshake.PassesAsync(addresses, host, port, token),
            resolveHostFallback: DnsOverHttpsResolver.ResolveAsync)
    {
    }

    /// <param name="browserHandshake">Проверка рукопожатия «как у браузера»; без неё шаг пропускается.</param>
    /// <param name="resolveHostFallback">Запасной DNS, если основной не нашёл адрес.</param>
    internal HttpStrategyProbe(
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost,
        Func<IPAddress[], SslProtocols, Action, HttpMessageHandler>? createHandler,
        IReadOnlyList<SslProtocols>? protocols,
        int? maxParallelRequests,
        TimeSpan? requestTimeout,
        Func<IPAddress[], string, int, CancellationToken, Task<bool>>? browserHandshake = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHostFallback = null)
    {
        _resolveHost = resolveHost ?? Dns.GetHostAddressesAsync;
        _resolveHostFallback = resolveHostFallback;
        _browserHandshake = browserHandshake ?? ((_, _, _, _) => Task.FromResult(true));
        _createHandler = createHandler ?? CreateDirectHandler;
        _protocols = protocols ?? GetSupportedProtocols();
        _maxParallelRequests = maxParallelRequests ?? 8;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    /// <summary>
    /// TLS 1.3 в Schannel есть только начиная с Windows 11 / Server 2022.
    /// На Windows 10 такие попытки всегда падают и лишь зашумляют результат.
    /// </summary>
    internal static IReadOnlyList<SslProtocols> GetSupportedProtocols()
    {
        return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)
            ? [SslProtocols.Tls12, SslProtocols.Tls13]
            : [SslProtocols.Tls12];
    }

    public async Task<IReadOnlyList<ProbeAttempt>> ProbeAsync(
        IReadOnlyList<StrategyTestTarget> targets,
        CancellationToken cancellationToken)
    {
        using var throttler = new SemaphoreSlim(_maxParallelRequests);
        var tasks = targets
            .SelectMany(target => _protocols.Select(protocol => ProbeThrottledAsync(target, protocol, throttler, cancellationToken)))
            .ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<ProbeAttempt> ProbeThrottledAsync(
        StrategyTestTarget target,
        SslProtocols protocol,
        SemaphoreSlim throttler,
        CancellationToken cancellationToken)
    {
        await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ProbeOnceAsync(target, protocol, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            throttler.Release();
        }
    }

    private async Task<ProbeAttempt> ProbeOnceAsync(
        StrategyTestTarget target,
        SslProtocols protocol,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveCachedAsync(target.Host, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return new ProbeAttempt(target, ProbeOutcome.DnsFailure, TimeSpan.Zero);
        }

        var (addresses, viaFallbackDns) = resolved;
        var certificateRejected = false;
        var stopwatch = Stopwatch.StartNew();
        if (target.Url.Scheme == Uri.UriSchemeHttps &&
            !await _browserHandshake(addresses, target.Host, target.Url.Port, cancellationToken).ConfigureAwait(false))
        {
            return new ProbeAttempt(target, ProbeOutcome.Failed, stopwatch.Elapsed, viaFallbackDns);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        stopwatch.Restart();
        try
        {
            // Новый handler на каждую попытку: соединения не переиспользуются,
            // и каждая попытка проходит полное TLS-рукопожатие через DPI.
            using var client = new HttpClient(
                _createHandler(addresses, protocol, () => certificateRejected = true),
                disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, target.Url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            var latency = stopwatch.Elapsed;
            await ReadBodyAsync(response.Content, timeout.Token).ConfigureAwait(false);
            return new ProbeAttempt(target, ProbeOutcome.Ok, latency, viaFallbackDns);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Любая сетевая ошибка (сброс, таймаут, обрыв тела, сбой TLS) — провал стратегии,
            // кроме отказа в сертификате: это подмена ответа, а не работа DPI.
            return new ProbeAttempt(
                target,
                certificateRejected ? ProbeOutcome.CertificateError : ProbeOutcome.Failed,
                stopwatch.Elapsed,
                viaFallbackDns);
        }
    }

    private static async Task ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            var total = 0;
            while (total < BodyLimitBytes)
            {
                var read = await stream
                    .ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, BodyLimitBytes - total)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private Task<ResolvedHost?> ResolveCachedAsync(string host, CancellationToken cancellationToken)
    {
        return _dnsCache
            .GetOrAdd(host, key => new Lazy<Task<ResolvedHost?>>(() => ResolveWithFallbackAsync(key, cancellationToken)))
            .Value;
    }

    private async Task<ResolvedHost?> ResolveWithFallbackAsync(string host, CancellationToken cancellationToken)
    {
        var addresses = await ResolveAsync(host, _resolveHost, cancellationToken).ConfigureAwait(false);
        if (addresses is not null)
        {
            return new ResolvedHost(addresses, ViaFallbackDns: false);
        }

        if (_resolveHostFallback is null)
        {
            return null;
        }

        var fallback = await ResolveAsync(host, _resolveHostFallback, cancellationToken).ConfigureAwait(false);
        return fallback is null ? null : new ResolvedHost(fallback, ViaFallbackDns: true);
    }

    private static async Task<IPAddress[]?> ResolveAsync(
        string host,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHost,
        CancellationToken cancellationToken)
    {
        // Одна повторная попытка: единичный сбой DNS не должен исключить цель на весь прогон.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var addresses = await resolveHost(host, cancellationToken)
                    .WaitAsync(DnsTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (addresses.Length > 0)
                {
                    // IPv4 первым: IPv6 часто есть в DNS, но не маршрутизируется.
                    return addresses
                        .OrderBy(address => address.AddressFamily == AddressFamily.InterNetworkV6 ? 1 : 0)
                        .ToArray();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or ArgumentException)
            {
            }
        }

        return null;
    }

    private sealed record ResolvedHost(IPAddress[] Addresses, bool ViaFallbackDns);

    private static HttpMessageHandler CreateDirectHandler(
        IPAddress[] addresses,
        SslProtocols protocol,
        Action onCertificateRejected)
    {
        return new SocketsHttpHandler
        {
            UseProxy = false,
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = ConnectTimeout,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = protocol,
                RemoteCertificateValidationCallback = (_, _, _, errors) =>
                {
                    if (errors != SslPolicyErrors.None)
                    {
                        onCertificateRejected();
                        return false;
                    }

                    return true;
                }
            },
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }
}
