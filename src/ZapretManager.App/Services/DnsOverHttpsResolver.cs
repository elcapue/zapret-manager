using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ZapretManager.App.Services;

/// <summary>
/// Запасной DNS для проверки стратегий. DNS провайдера бывает, что не находит заблокированные сайты
/// (NXDOMAIN для www.youtube.com), а браузеры при этом ходят через свой защищённый DNS.
/// Без запасного варианта такая цель выпадала бы из оценки целиком.
/// Серверы указаны по IP, чтобы не зависеть от того самого DNS провайдера.
/// </summary>
public static class DnsOverHttpsResolver
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);

    private static readonly string[] Endpoints =
    [
        "https://1.1.1.1/dns-query?type=A&name=",
        "https://8.8.8.8/resolve?type=A&name="
    ];

    public static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = RequestTimeout };
        foreach (var endpoint in Endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + Uri.EscapeDataString(host));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var addresses = ParseAnswers(json);
                if (addresses.Length > 0)
                {
                    return addresses;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
            }
        }

        return [];
    }

    /// <summary>Разбирает JSON-ответ формата dns-json: адреса из записей типа A (1).</summary>
    internal static IPAddress[] ParseAnswers(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Answer", out var answers) || answers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var addresses = new List<IPAddress>();
        foreach (var answer in answers.EnumerateArray())
        {
            if (answer.TryGetProperty("type", out var type) &&
                type.TryGetInt32(out var recordType) &&
                recordType == 1 &&
                answer.TryGetProperty("data", out var data) &&
                IPAddress.TryParse(data.GetString(), out var address))
            {
                addresses.Add(address);
            }
        }

        return [.. addresses];
    }
}
