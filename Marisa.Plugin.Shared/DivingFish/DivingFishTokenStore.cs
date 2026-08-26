using System.Collections.Concurrent;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth access token 缓存（按 sub + game 缓存）。
///     绑定由 DivingFishOAuthBind（QQ → sub）记录；换票用 on-behalf-of subject=sub。
/// </summary>
public static class DivingFishTokenStore
{
    // (sub, game) -> token
    private static readonly ConcurrentDictionary<(string Sub, string Game), DivingFishToken> Cache = new();

    // (sub, game) -> 刷新锁
    private static readonly ConcurrentDictionary<(string Sub, string Game), SemaphoreSlim> FetchLocks = new();

    /// <summary>
    ///     获取有效 token：查绑定表得 sub，再按需换票。
    ///     未绑定（无 verified 记录）返回 null。
    /// </summary>
    public static async Task<DivingFishToken?> GetValidToken(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var bind = realm.All<DivingFishOAuthBind>()
            .FirstOrDefault(x => x.Qq == qq && x.Status == "verified");
        if (bind == null || string.IsNullOrWhiteSpace(bind.Sub)) return null;

        var sub = bind.Sub;
        var key = (sub, game);

        // 1. 有票直接用
        if (Cache.TryGetValue(key, out var cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
        {
            return cached;
        }

        // 2. 无票 → 换票
        var fetchLock = FetchLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync();
        try
        {
            if (Cache.TryGetValue(key, out cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
            {
                return cached;
            }

            var token = await DivingFishOAuth.FetchToken($"sub:{sub}", game);
            Cache[key] = token;
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

    /// <summary>
    ///     令牌失效（401）时清除缓存，下次请求重新换票
    /// </summary>
    public static void RemoveToken(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var bind = realm.All<DivingFishOAuthBind>()
            .FirstOrDefault(x => x.Qq == qq && x.Status == "verified");
        if (bind == null) return;

        Cache.TryRemove((bind.Sub, game), out _);
    }
}
