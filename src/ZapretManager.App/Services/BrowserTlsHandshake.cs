using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ZapretManager.App.Services;

/// <summary>
/// Проверяет, пропускает ли DPI TLS-подключение «как у браузера».
///
/// Schannel на Windows 10 умеет только TLS 1.2 и шлёт маленький ClientHello, а Chrome, Firefox и
/// Discord (Electron) — TLS 1.3 с приветствием около 1,8 КБ, которое уходит двумя TCP-сегментами.
/// Часть стратегий пропускает первое и глушит второе: HTTPS-проверка через Schannel такие
/// стратегии не отличает от рабочих (так было с SIMPLE FAKE для Discord).
///
/// Рукопожатие до конца не доводится: DPI решает по ClientHello, поэтому любой TLS-ответ сервера
/// (ServerHello или alert) означает, что подключение прошло. Таймаут, сброс или закрытие — нет.
/// </summary>
public static class BrowserTlsHandshake
{
    private const int ChromeHandshakeLength = 1780;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static async Task<bool> PassesAsync(
        IReadOnlyList<IPAddress> addresses,
        string host,
        int port,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? DefaultTimeout);
        try
        {
            await socket.ConnectAsync(addresses.ToArray(), port, deadline.Token).ConfigureAwait(false);
            await socket.SendAsync(BuildClientHello(host), SocketFlags.None, deadline.Token).ConfigureAwait(false);

            var header = new byte[5];
            var received = 0;
            while (received < header.Length)
            {
                var read = await socket
                    .ReceiveAsync(header.AsMemory(received), SocketFlags.None, deadline.Token)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                received += read;
            }

            return IsTlsResponse(header);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException)
        {
            return false;
        }
    }

    /// <summary>Заголовок TLS-записи от сервера: handshake (0x16) или alert (0x15), версия 3.x.</summary>
    internal static bool IsTlsResponse(ReadOnlySpan<byte> header)
    {
        return header.Length >= 3 && header[0] is 0x16 or 0x15 && header[1] == 0x03;
    }

    /// <summary>
    /// ClientHello в духе Chrome: TLS 1.3 + 1.2, ALPN h2/http1.1, ключ X25519 и padding до ~1,8 КБ —
    /// такого размера приветствие Chrome с постквантовым ключом X25519MLKEM768.
    /// </summary>
    internal static byte[] BuildClientHello(string host)
    {
        var body = new List<byte> { 0x03, 0x03 };
        body.AddRange(RandomNumberGenerator.GetBytes(32));
        body.Add(32);
        body.AddRange(RandomNumberGenerator.GetBytes(32));

        ushort[] cipherSuites =
        [
            0x1301, 0x1302, 0x1303, 0xc02b, 0xc02f, 0xc02c, 0xc030,
            0xcca9, 0xcca8, 0xc013, 0xc014, 0x009c, 0x009d, 0x002f, 0x0035
        ];
        WriteUInt16(body, cipherSuites.Length * 2);
        foreach (var suite in cipherSuites)
        {
            WriteUInt16(body, suite);
        }

        body.Add(0x01);
        body.Add(0x00);

        var extensions = new List<byte>();
        var hostBytes = Encoding.ASCII.GetBytes(host);
        var serverName = new List<byte>();
        WriteUInt16(serverName, hostBytes.Length + 3);
        serverName.Add(0x00);
        WriteUInt16(serverName, hostBytes.Length);
        serverName.AddRange(hostBytes);
        WriteExtension(extensions, 0x0000, serverName);
        WriteExtension(extensions, 0x0017, []);
        WriteExtension(extensions, 0xff01, [0x00]);
        WriteExtension(extensions, 0x000a, [0x00, 0x06, 0x00, 0x1d, 0x00, 0x17, 0x00, 0x18]);
        WriteExtension(extensions, 0x000b, [0x01, 0x00]);
        WriteExtension(extensions, 0x0023, []);
        WriteExtension(extensions, 0x0010, [0x00, 0x0c, 0x02, (byte)'h', (byte)'2', 0x08, .. "http/1.1"u8.ToArray()]);
        WriteExtension(extensions, 0x0005, [0x01, 0x00, 0x00, 0x00, 0x00]);
        WriteExtension(
            extensions,
            0x000d,
            [0x00, 0x10, 0x04, 0x03, 0x08, 0x04, 0x04, 0x01, 0x05, 0x03, 0x08, 0x05, 0x05, 0x01, 0x08, 0x06, 0x06, 0x01]);
        WriteExtension(extensions, 0x0012, []);

        var keyShare = new List<byte>();
        WriteUInt16(keyShare, 36);
        WriteUInt16(keyShare, 0x001d);
        WriteUInt16(keyShare, 32);
        keyShare.AddRange(RandomNumberGenerator.GetBytes(32));
        WriteExtension(extensions, 0x0033, keyShare);
        WriteExtension(extensions, 0x002d, [0x01, 0x01]);
        WriteExtension(extensions, 0x002b, [0x04, 0x03, 0x04, 0x03, 0x03]);
        WriteExtension(extensions, 0x001b, [0x02, 0x00, 0x02]);

        // Заголовок handshake (4) + тело + длина расширений (2) + заголовок padding (4).
        var paddingLength = ChromeHandshakeLength - (4 + body.Count + 2 + extensions.Count) - 4;
        if (paddingLength > 0)
        {
            WriteExtension(extensions, 0x0015, new byte[paddingLength]);
        }

        WriteUInt16(body, extensions.Count);
        body.AddRange(extensions);

        var record = new List<byte>(body.Count + 9) { 0x16, 0x03, 0x01 };
        WriteUInt16(record, body.Count + 4);
        record.Add(0x01);
        record.Add((byte)(body.Count >> 16));
        record.Add((byte)(body.Count >> 8));
        record.Add((byte)body.Count);
        record.AddRange(body);
        return [.. record];
    }

    private static void WriteExtension(List<byte> target, int type, IReadOnlyCollection<byte> data)
    {
        WriteUInt16(target, type);
        WriteUInt16(target, data.Count);
        target.AddRange(data);
    }

    private static void WriteUInt16(List<byte> target, int value)
    {
        target.Add((byte)(value >> 8));
        target.Add((byte)value);
    }
}
