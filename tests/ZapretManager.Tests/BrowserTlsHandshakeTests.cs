using System.Net;
using System.Text;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class BrowserTlsHandshakeTests
{
    [Fact]
    public void BuildClientHello_LooksLikeChromeTls13Hello()
    {
        var hello = BrowserTlsHandshake.BuildClientHello("discord.com");

        Assert.Equal(0x16, hello[0]);
        Assert.Equal(0x01, hello[5]);
        // Размер как у Chrome с постквантовым ключом: больше одного TCP-сегмента (MSS 1460).
        Assert.InRange(hello.Length, 1500, 2000);
        Assert.Equal(hello.Length - 5, (hello[3] << 8) | hello[4]);
        Assert.Equal(hello.Length - 9, (hello[6] << 16) | (hello[7] << 8) | hello[8]);
        Assert.Contains("discord.com", Encoding.ASCII.GetString(hello));
        // supported_versions с TLS 1.3.
        Assert.True(ContainsSequence(hello, [0x00, 0x2b, 0x00, 0x05, 0x04, 0x03, 0x04]));
    }

    [Theory]
    [InlineData(new byte[] { 0x16, 0x03, 0x03, 0x00, 0x7a }, true)]
    [InlineData(new byte[] { 0x15, 0x03, 0x03, 0x00, 0x02 }, true)]
    [InlineData(new byte[] { 0x48, 0x54, 0x54, 0x50, 0x2f }, false)]
    public void IsTlsResponse_AcceptsServerHelloAndAlert(byte[] header, bool expected)
    {
        Assert.Equal(expected, BrowserTlsHandshake.IsTlsResponse(header));
    }

    [Fact]
    public async Task PassesAsync_WhenNothingListens_ReturnsFalse()
    {
        var passed = await BrowserTlsHandshake.PassesAsync(
            [IPAddress.Loopback],
            "example.com",
            port: 1,
            CancellationToken.None,
            TimeSpan.FromSeconds(2));

        Assert.False(passed);
    }

    [Fact]
    public void DnsOverHttps_ParseAnswers_ReturnsOnlyARecords()
    {
        const string json = """
            {"Status":0,"Answer":[
              {"name":"www.youtube.com","type":5,"data":"youtube-ui.l.google.com."},
              {"name":"youtube-ui.l.google.com","type":1,"data":"142.251.153.4"},
              {"name":"youtube-ui.l.google.com","type":1,"data":"142.251.151.4"}]}
            """;

        var addresses = DnsOverHttpsResolver.ParseAnswers(json);

        Assert.Equal([IPAddress.Parse("142.251.153.4"), IPAddress.Parse("142.251.151.4")], addresses);
    }

    [Fact]
    public void DnsOverHttps_ParseAnswers_WithoutAnswerSection_ReturnsEmpty()
    {
        Assert.Empty(DnsOverHttpsResolver.ParseAnswers("""{"Status":3}"""));
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var index = 0; index <= data.Length - sequence.Length; index++)
        {
            if (data.AsSpan(index, sequence.Length).SequenceEqual(sequence))
            {
                return true;
            }
        }

        return false;
    }
}
