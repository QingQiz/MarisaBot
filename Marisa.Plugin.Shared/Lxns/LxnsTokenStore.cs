using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.Lxns;

public static class LxnsTokenStore
{
    private static readonly object LockObj = new();
    private static Dictionary<long, LxnsTokenRecord>? _cache;

    // 可被测试重定向；生产环境保持默认（chunithm temp 目录）
    private static string? _storePath;

    private static string StorePath => _storePath ??= Path.Combine(
        ConfigurationManager.Configuration.Chunithm.TempPath, "lxns_oauth_tokens.json");

    // 每个 qq 一把刷新锁：防止并发刷新用同一 refresh token（lxns 刷新会轮换 token，旧 token 立即失效）
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> RefreshLocks = new();

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
        lock (LockObj)
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

        // access token 未过期（留 60 秒余量）直接复用，避免频繁刷新
        if (DateTime.UtcNow < token.ExpiresAt.AddSeconds(-60))
        {
            return token;
        }

        // 同一 qq 的刷新串行化：并发查询时只有一个能 refresh，其余等待后复用新 token
        var refreshLock = RefreshLocks.GetOrAdd(qq, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync();
        try
        {
            // 等待期间可能已被其他请求刷新，先复查
            token = GetToken(qq);
            if (token == null) return null;
            if (DateTime.UtcNow < token.ExpiresAt.AddSeconds(-60))
            {
                return token;
            }

            try
            {
                token = await LxnsOAuth.RefreshToken(token.RefreshToken);
                SaveToken(qq, token.AccessToken, token.RefreshToken,
                    (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                return token;
            }
            catch (Exception e)
            {
                // 仅当明确是 token 失效（400/401）时才删除；网络/服务器错误保留 token，避免误删
                if (e is HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized })
                {
                    RemoveToken(qq);
                }
                throw;
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public static void RemoveToken(long qq)
    {
        lock (LockObj)
        {
            var store = Load();
            store.Remove(qq);
            Save();
        }
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
