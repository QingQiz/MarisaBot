using System.Collections.Concurrent;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth access token 缓存。
///     换票按应用对用户每小时限 60 次，令牌 5 分钟有效，需在有效期内复用。
///     缓存键 = (qq, game)：scope 按游戏区分，同一用户不同游戏需分别换票。
/// </summary>
public static class DivingFishTokenStore
{
    // (qq, game) -> token
    private static readonly ConcurrentDictionary<(long Qq, string Game), DivingFishToken> Cache = new();

    // (qq, game) -> 刷新锁：并发查询时同一键只有一个能换票，其余等待复用
    private static readonly ConcurrentDictionary<(long Qq, string Game), SemaphoreSlim> FetchLocks = new();

    /// <summary>
    ///     获取有效 token，按需换票：
    ///     1. 缓存有未过期 token → 直接复用（不换票）
    ///     2. 缓存无/过期 → 换票试探绑定：已绑定则换票成功返回 token；未绑定返回 null
    ///     3. 换票因限流/网络/凭据错误失败 → 抛出原异常（≠ 未绑定，调用方不应引导绑定）
    /// </summary>
    public static async Task<DivingFishToken?> GetValidToken(long qq, string game)
    {
        var key = (qq, game);

        // 1. 有票直接用
        if (Cache.TryGetValue(key, out var cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
        {
            return cached;
        }

        // 2. 无票 → 换票（检测是否绑定）
        var fetchLock = FetchLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync();
        try
        {
            // 等待期间可能已被其他请求换票，先复查
            if (Cache.TryGetValue(key, out cached) && DateTime.UtcNow < cached.ExpiresAt.AddSeconds(-30))
            {
                return cached;
            }

            var token = await DivingFishOAuth.FetchTokenByQq(qq, game);
            Cache[key] = token;
            return token;
        }
        catch (DivingFishNotBoundException)
        {
            // 未绑定（或账号不存在）：返回 null，由调用方引导绑定
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
        Cache.TryRemove((qq, game), out _);
    }
}
