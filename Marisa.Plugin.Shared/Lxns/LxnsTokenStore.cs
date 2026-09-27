using System.Collections.Concurrent;
using System.Net;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Lxns;
using Realms;

namespace Marisa.Plugin.Shared.Lxns;

/// <summary>
///     落雪 OAuth 授权与票据存取：持久化在 <see cref="LxnsAuthToken" /> 表中，
///     以 QQ 直接定位（令牌是账号级的，与游戏无关），不依赖游戏绑定状态。
/// </summary>
public static class LxnsTokenStore
{
    // 每个 qq 一把刷新锁：防止并发刷新用同一 refresh token（lxns 刷新会轮换 token，旧 token 立即失效）
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> RefreshLocks = new();

    public static void SaveToken(long qq, string accessToken, string refreshToken, int expiresIn)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq) ?? realm.AddWithAutoId(new LxnsAuthToken { Qq = qq });
        row.AccessToken = accessToken;
        row.RefreshToken = refreshToken;
        row.ExpiresAt = expiresAt;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        tr.Commit();
    }

    public static LxnsToken? GetToken(long qq)
    {
        var row = ReadRow(qq);
        return row == null ? null : ToToken(row);
    }

    public static async Task<LxnsToken?> GetValidToken(long qq)
    {
        var token = GetToken(qq);
        if (token == null) return null;

        // access token 未过期（留 60 秒余量）直接复用，避免频繁刷新
        if (IsFresh(token)) return token;

        // 同一 qq 的刷新串行化：并发查询时只有一个能 refresh，其余等待后复用新 token
        var refreshLock = RefreshLocks.GetOrAdd(qq, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync();
        try
        {
            // 等待期间可能已被其他请求刷新，先复查
            token = GetToken(qq);
            if (token == null) return null;
            if (IsFresh(token)) return token;

            try
            {
                token = await LxnsOAuth.RefreshToken(token.RefreshToken);
                SaveToken(qq, token.AccessToken, token.RefreshToken,
                    (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                return token;
            }
            catch (Exception e)
            {
                // 仅当明确是 token 失效（400/401）时才删除授权；网络/服务器错误保留，避免误删
                if (e is HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized })
                {
                    RemoveAuthorization(qq);
                }
                throw;
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    /// <summary>访问令牌被服务端拒绝（401/403）时调用：只丢票据、保留刷新令牌，下次查询自动刷新。</summary>
    public static void RemoveToken(long qq)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq);
        if (row != null)
        {
            row.AccessToken = "";
            row.ExpiresAt = default;
        }
        tr.Commit();
    }

    /// <summary>刷新令牌也失效时调用：整行删除，用户需要重新授权。</summary>
    private static void RemoveAuthorization(long qq)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq);
        if (row != null) realm.Remove(row);
        tr.Commit();
    }

    private static RowSnapshot? ReadRow(long qq)
    {
        using var realm = BotDbContext.OpenRealm();
        var row = Find(realm, qq);
        return row == null ? null : new RowSnapshot(row.AccessToken, row.RefreshToken, row.ExpiresAt);
    }

    private static LxnsAuthToken? Find(Realm realm, long qq)
    {
        return realm.All<LxnsAuthToken>().FirstOrDefault(x => x.Qq == qq);
    }

    private static bool IsFresh(LxnsToken token)
    {
        return !string.IsNullOrWhiteSpace(token.AccessToken) &&
               DateTime.UtcNow < token.ExpiresAt.AddSeconds(-60);
    }

    private static LxnsToken ToToken(RowSnapshot row)
    {
        return new LxnsToken
        {
            AccessToken = row.AccessToken,
            RefreshToken = row.RefreshToken,
            ExpiresAt = row.ExpiresAt.UtcDateTime
        };
    }

    private sealed record RowSnapshot(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
}
