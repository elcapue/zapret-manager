using System.Security.Cryptography;

namespace ZapretManager.App.Services;

/// <summary>Проверка SHA-256 скачанного файла по полю <c>digest</c> из GitHub Releases API («sha256:…»).</summary>
public static class ReleaseDigest
{
    private const string Sha256Prefix = "sha256:";

    /// <param name="required">
    /// true — релиз без sha256-digest отклоняется. Так проверяется сам менеджер: он заменяет свой exe.
    /// </param>
    public static bool Matches(byte[] bytes, string? digest, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return !required;
        }

        var expected = digest[Sha256Prefix.Length..].Trim();
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}
