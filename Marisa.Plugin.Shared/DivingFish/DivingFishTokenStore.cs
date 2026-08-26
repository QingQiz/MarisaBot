using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth access token 缓存（按 sub + game 缓存）。
///     绑定由 DivingFishOAuthBind（QQ → sub）记录；换票用 on-behalf-of subject=sub。
///     存量用户（旧设备码绑定，水鱼侧已有 ref 映射）在无 verified 绑定时，
///     先用 ref 换票检测，成功后自动迁移 sub 到绑定表（status=unverified）。
/// </summary>
public static class DivingFishTokenStore
{
    // (sub, game) -> token
    private static readonly ConcurrentDictionary<(string Sub, string Game), DivingFishToken> Cache = new();

    // (sub, game) -> 刷新锁
    private static readonly ConcurrentDictionary<(string Sub, string Game), SemaphoreSlim> FetchLocks = new();

    /// <summary>
    ///     获取有效 token：
    ///     1. 有绑定（verified 或已迁移的 unverified）→ 用 sub 换票；
    ///     2. 无绑定 → 用旧 ref 映射探测（存量迁移），成功则提取 sub 存入绑定表；
    ///     3. 都失败（未绑定）返回 null。
    /// </summary>
    public static async Task<DivingFishToken?> GetValidToken(long qq, string game)
    {
        string? sub = null;

        // 1. 查已绑定的 sub（verified 或存量迁移的 unverified 都可用）
        using (var realm = BotDbContext.OpenRealm())
        {
            var bind = realm.All<DivingFishOAuthBind>()
                .FirstOrDefault(x => x.Qq == qq && x.Sub.Length > 0);
            if (bind != null && !string.IsNullOrWhiteSpace(bind.Sub))
            {
                sub = bind.Sub;
            }
        }

        // 2. 有 sub → 正常换票
        if (sub != null)
        {
            return await FetchTokenForSub(sub, game, qq);
        }

        // 3. 无绑定 → 存量迁移检测：用旧 ref 映射换票，成功后提取 sub 持久化
        return await FetchTokenForSub(null, game, qq, useRef: true);
    }

    private static async Task<DivingFishToken?> FetchTokenForSub(
        string? sub, string game, long qq, bool useRef = false)
    {
        string subject;
        if (useRef)
        {
            subject = "ref:" + DivingFishOAuth.SubjectRef(qq.ToString());
        }
        else
        {
            subject = "sub:" + sub;
        }

        var cacheKey = (subject, game);

        // 有票直接用
        if (Cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
        {
            return cached;
        }

        var fetchLock = FetchLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync();
        try
        {
            if (Cache.TryGetValue(cacheKey, out cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
            {
                return cached;
            }

            var token = await DivingFishOAuth.FetchToken(subject, game);

            // 存量迁移：ref 换票成功后提取 sub，写入绑定表（unverified），下次走 sub
            if (useRef)
            {
                var extractedSub = ExtractSub(token.AccessToken);
                if (!string.IsNullOrWhiteSpace(extractedSub))
                {
                    PersistMigratedBind(qq, extractedSub, game);
                    Cache.TryRemove(cacheKey, out _);
                    cacheKey = ("sub:" + extractedSub, game);
                }
            }

            Cache[cacheKey] = token;
            return token;
        }
        catch (DivingFishNotBoundException)
        {
            return null;
        }
        finally
        {
            fetchLock.Release();
        }
    }

    /// <summary>从 access token JWT 的 payload 提取 sub（水鱼用户 ID）</summary>
    private static string? ExtractSub(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            while (payload.Length % 4 != 0) payload += '=';

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var s) ? s.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>存量迁移：把 QQ → sub 写入绑定表（unverified，供后续正式确认）</summary>
    private static void PersistMigratedBind(long qq, string sub, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var existing = realm.All<DivingFishOAuthBind>().FirstOrDefault(x => x.Qq == qq);
        realm.Write(() =>
        {
            if (existing == null)
            {
                realm.AddWithAutoId(new DivingFishOAuthBind
                {
                    Qq = qq,
                    Sub = sub,
                    Username = "",
                    Scopes = DivingFishOAuth.ScopeOf(game),
                    Status = "unverified",
                    VerifiedAt = DateTimeOffset.Now
                });
            }
            else if (existing.Status != "verified")
            {
                existing.Sub = sub;
                existing.Scopes = DivingFishOAuth.ScopeOf(game);
            }
        });
    }

    /// <summary>
    ///     令牌失效（401）时清除缓存，下次请求重新换票
    /// </summary>
    public static void RemoveToken(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var bind = realm.All<DivingFishOAuthBind>()
            .FirstOrDefault(x => x.Qq == qq);
        if (bind == null || string.IsNullOrWhiteSpace(bind.Sub)) return;

        Cache.TryRemove(("sub:" + bind.Sub, game), out _);
    }
}
