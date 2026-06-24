using System.Text.Json;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.Lxns;

public static class LxnsTokenStore
{
    private static readonly string StorePath = Path.Combine(
        ConfigurationManager.Configuration.Chunithm.TempPath, "lxns_oauth_tokens.json");

    private static Dictionary<long, LxnsTokenRecord>? _cache;

    private static Dictionary<long, LxnsTokenRecord> Load()
    {
        if (_cache != null) return _cache;
        if (File.Exists(StorePath))
        {
            var json = File.ReadAllText(StorePath);
            _cache = JsonSerializer.Deserialize<Dictionary<long, LxnsTokenRecord>>(json) ?? new();
        }
        else
        {
            _cache = new();
        }
        return _cache;
    }

    private static void Save()
    {
        var dir = Path.GetDirectoryName(StorePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_cache);
        File.WriteAllText(StorePath, json);
    }

    public static void SaveToken(long qq, string accessToken, string refreshToken, int expiresIn)
    {
        var store = Load();
        store[qq] = new LxnsTokenRecord
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };
        Save();
    }

    public static LxnsToken? GetToken(long qq)
    {
        var store = Load();
        if (!store.TryGetValue(qq, out var record)) return null;
        return new LxnsToken
        {
            AccessToken = record.AccessToken,
            RefreshToken = record.RefreshToken,
            ExpiresAt = record.ExpiresAt
        };
    }

    public static async Task<LxnsToken?> GetValidToken(long qq)
    {
        var token = GetToken(qq);
        if (token == null) return null;

        if (token.IsExpired)
        {
            try
            {
                token = await LxnsOAuth.RefreshToken(token.RefreshToken);
                SaveToken(qq, token.AccessToken, token.RefreshToken,
                    (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);
            }
            catch
            {
                return null;
            }
        }

        return token;
    }

    public static void RemoveToken(long qq)
    {
        var store = Load();
        store.Remove(qq);
        Save();
    }

    public static void Invalidate()
    {
        _cache = null;
    }

    private class LxnsTokenRecord
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}
