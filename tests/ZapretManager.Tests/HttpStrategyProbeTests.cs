using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class HttpStrategyProbeTests
{
    private static readonly StrategyTestTarget YouTube =
        StrategyTestTarget.TryCreate("YouTube", "https://yt.example", StrategyTargetGroup.YouTube)!;
    private static readonly StrategyTestTarget Discord =
        StrategyTestTarget.TryCreate("Discord", "https://dc.example", StrategyTargetGroup.Discord)!;

    [Fact]
    public async Task ProbeAsync_AnyHttpStatusMeansConnectionPassedDpi()
    {
        var probe = CreateProbe((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new ByteArrayContent(new byte[1500])
        }));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.Ok, attempt.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_WhenConnectionIsReset_ReportsFailed()
    {
        var probe = CreateProbe((_, _) => throw new HttpRequestException(
            "reset",
            new IOException("reset", new SocketException((int)SocketError.ConnectionReset))));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.Failed, attempt.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_WhenBodyStallsAfterFewKilobytes_ReportsFailed()
    {
        // Типичная блокировка ТСПУ: сервер ответил, но передача замирает на ~16 КБ.
        var probe = CreateProbe(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream(bytesBeforeStall: 16 * 1024))
            }),
            requestTimeout: TimeSpan.FromMilliseconds(200));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.Failed, attempt.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_ReadsOnlyUpToBodyLimit()
    {
        var stream = new StallingStream(bytesBeforeStall: int.MaxValue);
        var probe = CreateProbe((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.Ok, attempt.Outcome);
        Assert.Equal(HttpStrategyProbe.BodyLimitBytes, stream.BytesRead);
    }

    [Fact]
    public async Task ProbeAsync_WhenCertificateIsRejected_ReportsCertificateError()
    {
        var probe = new HttpStrategyProbe(
            resolveHost: Resolve,
            createHandler: (_, _, onCertificateRejected) => new StubHandler((_, _) =>
            {
                onCertificateRejected();
                throw new HttpRequestException("ssl", new AuthenticationException("remote certificate is invalid"));
            }),
            protocols: [SslProtocols.Tls12],
            maxParallelRequests: 4,
            requestTimeout: TimeSpan.FromSeconds(5));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.CertificateError, attempt.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_ResolvesEachHostOnceAndReportsDnsFailures()
    {
        var resolveCalls = new List<string>();
        var requests = 0;
        var probe = new HttpStrategyProbe(
            resolveHost: (host, _) =>
            {
                lock (resolveCalls)
                {
                    resolveCalls.Add(host);
                }

                return host == "yt.example"
                    ? Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound))
                    : Task.FromResult(new[] { IPAddress.Loopback });
            },
            createHandler: (_, _, _) => new StubHandler((_, _) =>
            {
                Interlocked.Increment(ref requests);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }),
            protocols: [SslProtocols.Tls12, SslProtocols.Tls13],
            maxParallelRequests: 4,
            requestTimeout: TimeSpan.FromSeconds(5));

        var first = await probe.ProbeAsync([YouTube, Discord], CancellationToken.None);
        var second = await probe.ProbeAsync([YouTube, Discord], CancellationToken.None);

        Assert.Equal(4, first.Count);
        Assert.All(first.Concat(second).Where(attempt => attempt.Target == YouTube), attempt => Assert.Equal(ProbeOutcome.DnsFailure, attempt.Outcome));
        Assert.All(first.Concat(second).Where(attempt => attempt.Target == Discord), attempt => Assert.Equal(ProbeOutcome.Ok, attempt.Outcome));
        // Неудачный резолв повторяется один раз, дальше результат берётся из кэша.
        Assert.Equal(2, resolveCalls.Count(host => host == "yt.example"));
        Assert.Equal(1, resolveCalls.Count(host => host == "dc.example"));
        Assert.Equal(4, requests);
    }

    [Fact]
    public async Task ProbeAsync_WhenBrowserHandshakeIsBlocked_FailsEvenIfSchannelRequestWorks()
    {
        var requests = 0;
        var probe = new HttpStrategyProbe(
            resolveHost: Resolve,
            createHandler: (_, _, _) => new StubHandler((_, _) =>
            {
                Interlocked.Increment(ref requests);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }),
            protocols: [SslProtocols.Tls12],
            maxParallelRequests: 4,
            requestTimeout: TimeSpan.FromSeconds(5),
            browserHandshake: (_, host, _, _) => Task.FromResult(host != "dc.example"));

        var attempts = await probe.ProbeAsync([YouTube, Discord], CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, attempts.Single(attempt => attempt.Target == YouTube).Outcome);
        Assert.Equal(ProbeOutcome.Failed, attempts.Single(attempt => attempt.Target == Discord).Outcome);
        // Заблокированное рукопожатие не тратит время на HTTPS-запрос.
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task ProbeAsync_WhenSystemDnsFails_UsesFallbackDnsAndMarksAttempt()
    {
        var probe = new HttpStrategyProbe(
            resolveHost: (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)),
            createHandler: (_, _, _) => new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            protocols: [SslProtocols.Tls12],
            maxParallelRequests: 4,
            requestTimeout: TimeSpan.FromSeconds(5),
            resolveHostFallback: (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        var attempt = Assert.Single(await probe.ProbeAsync([YouTube], CancellationToken.None));

        Assert.Equal(ProbeOutcome.Ok, attempt.Outcome);
        Assert.True(attempt.ResolvedViaFallbackDns);
    }

    [Fact]
    public async Task ProbeAsync_WhenUserCancels_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = CreateProbe(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ProbeAsync([YouTube], cancellation.Token));
    }

    [Fact]
    public void GetSupportedProtocols_AlwaysIncludesTls12()
    {
        Assert.Contains(SslProtocols.Tls12, HttpStrategyProbe.GetSupportedProtocols());
    }

    private static HttpStrategyProbe CreateProbe(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        TimeSpan? requestTimeout = null)
    {
        return new HttpStrategyProbe(
            resolveHost: Resolve,
            createHandler: (_, _, _) => new StubHandler(send),
            protocols: [SslProtocols.Tls12],
            maxParallelRequests: 4,
            requestTimeout: requestTimeout ?? TimeSpan.FromSeconds(5));
    }

    private static Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken)
    {
        return Task.FromResult(new[] { IPAddress.Loopback });
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _send(request, cancellationToken);
        }
    }

    /// <summary>Отдаёт заданное число байт, после чего «зависает» до отмены.</summary>
    private sealed class StallingStream : Stream
    {
        private readonly int _bytesBeforeStall;

        public StallingStream(int bytesBeforeStall)
        {
            _bytesBeforeStall = bytesBeforeStall;
        }

        public int BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var available = _bytesBeforeStall - BytesRead;
            if (available <= 0)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            var count = Math.Min(buffer.Length, available);
            BytesRead += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
