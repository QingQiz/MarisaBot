using System.Collections.Concurrent;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.Lxns;

public static class ShortUrlStore
{
    private static readonly ConcurrentDictionary<string, Entry> Store = new();
    private static readonly char[] Chars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    public static string CreateShortUrl(string url, TimeSpan? ttl = null)
    {
        ttl ??= TimeSpan.FromMinutes(30);
        var code = GenerateCode(6);
        Store[code] = new Entry(url, DateTime.UtcNow + ttl.Value);
        return code;
    }

    public static string? GetUrl(string code)
    {
        if (Store.TryGetValue(code, out var entry))
        {
            if (DateTime.UtcNow < entry.ExpiresAt)
                return entry.Url;

            Store.TryRemove(code, out _);
        }
        return null;
    }

    /// <summary>
    /// 从配置中的 web.public 构造完整公网地址
    /// </summary>
    public static string GetPublicBaseUrl()
    {
        var publicField = ConfigurationManager.Configuration.Web?.Public;
        if (string.IsNullOrWhiteSpace(publicField))
            return "http://localhost";

        publicField = publicField.Trim();
        if (!publicField.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !publicField.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "https://" + publicField;

        return publicField;
    }

    /// <summary>
    /// 获取短链的完整 URL
    /// </summary>
    public static string GetShortUrl(string code) => $"{GetPublicBaseUrl()}/go/{code}";

    private static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Chars[Random.Shared.Next(Chars.Length)];
        return new string(chars);
    }

    private record Entry(string Url, DateTime ExpiresAt);
}
