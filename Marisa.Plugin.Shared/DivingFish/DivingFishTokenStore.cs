using System.Collections.Concurrent;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth access token 缓存（按 sub + game 缓存）。
///     绑定由 DivingFishOAuthBind（QQ → sub + refresh_token）记录；
///     日常查分用 refresh_token 刷新（grant_type=refresh_token，授权码接入方式下无 on-behalf-of）。
///     水鱼 refresh token 强制轮换：刷新串行化（每 sub 一把锁），并原子持久化新 token。
/// </summary>
public static class DivingFishTokenStore
{
    // (sub, game) -> token
    private static readonly ConcurrentDictionary<(string Sub, string Game), DivingFishToken> Cache = new();

    // (sub) -> 刷新锁：并发查询时同一用户只能有一个刷新，防 refresh token 轮换冲突
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FetchLocks = new();

    /// <summary>
    ///     获取有效 token：
    ///     1. 查绑定表得 sub + refresh_token；
    ///     2. 无绑定 → null；
    ///     3. 有缓存 access token 未过期 → 复用；
    ///     4. 否则用 refresh_token 刷新，并持久化新 refresh_token（强制轮换）。
    /// </summary>
    public static async Task<DivingFishToken?> GetValidToken(long qq, string game)
    {
        string? sub;
        string? refreshToken;
        using (var realm = BotDbContext.OpenRealm())
        {
            var bind = realm.All<DivingFishOAuthBind>()
                .FirstOrDefault(x => x.Qq == qq && x.Sub != "");
            if (bind == null || string.IsNullOrWhiteSpace(bind.Sub)) return null;
            sub = bind.Sub;
            refreshToken = bind.RefreshToken;
        }

        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var key = (sub, game);

        // 1. 有票直接用
        if (Cache.TryGetValue(key, out var cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
        {
            return cached;
        }

        // 2. 无票 → 刷新（按 sub 串行化）
        var fetchLock = FetchLocks.GetOrAdd(sub, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync();
        try
        {
            if (Cache.TryGetValue(key, out cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
            {
                return cached;
            }

            // 刷新前重新读 refresh_token（可能已被其他请求轮换）
            using (var realm = BotDbContext.OpenRealm())
            {
                var bind = realm.All<DivingFishOAuthBind>().FirstOrDefault(x => x.Qq == qq && x.Sub != "");
                if (bind != null && !string.IsNullOrWhiteSpace(bind.RefreshToken))
                {
                    refreshToken = bind.RefreshToken;
                }
            }

            var token = await DivingFishOAuth.RefreshToken(refreshToken);
            Cache[key] = token;

            // 持久化新 refresh_token（强制轮换：旧 token 已作废）
            using (var realm = BotDbContext.OpenRealm())
            {
                var bind = realm.All<DivingFishOAuthBind>().FirstOrDefault(x => x.Qq == qq && x.Sub != "");
                if (bind != null)
                {
                    realm.Write(() =>
                    {
                        bind.RefreshToken = token.RefreshToken;
                        bind.Scopes = DivingFishOAuth.ScopeOf(game);
                    });
                }
            }

            return token;
        }
        finally
        {
            fetchLock.Release();
        }
    }

    /// <summary>
    ///     令牌失效（401）时清除缓存，下次请求重新刷新
    /// </summary>
    public static void RemoveToken(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var bind = realm.All<DivingFishOAuthBind>()
            .FirstOrDefault(x => x.Qq == qq && x.Sub != "");
        if (bind == null || string.IsNullOrWhiteSpace(bind.Sub)) return;

        Cache.TryRemove((bind.Sub, game), out _);
    }
}
