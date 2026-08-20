namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth access token 缓存。
///     换票按用户每小时限 60 次，令牌 5 分钟有效，需在有效期内复用。
/// </summary>
public static class DivingFishTokenStore
{
    private static readonly object LockObj = new();

    // qq -> token
    private static readonly Dictionary<long, DivingFishToken> Cache = new();

    /// <summary>
    ///     获取有效 token：缓存有效则复用，否则现场换票（未绑定返回 null）
    /// </summary>
    public static async Task<DivingFishToken?> GetValidToken(long qq)
    {
        lock (LockObj)
        {
            // 留 30 秒余量
            if (Cache.TryGetValue(qq, out var cached) && !cached.IsExpired)
            {
                return cached;
            }
        }

        try
        {
            var token = await DivingFishOAuth.FetchTokenByQq(qq);
            lock (LockObj)
            {
                Cache[qq] = token;
            }
            return token;
        }
        catch (DivingFishNotBoundException)
        {
            return null;
        }
    }

    /// <summary>
    ///     令牌失效（401）时清除缓存，下次请求重新换票
    /// </summary>
    public static void RemoveToken(long qq)
    {
        lock (LockObj)
        {
            Cache.Remove(qq);
        }
    }
}
