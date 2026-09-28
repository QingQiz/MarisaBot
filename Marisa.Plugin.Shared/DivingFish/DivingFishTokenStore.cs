using System.Collections.Concurrent;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;
using Realms;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼 OAuth 票据存取：授权与票据都持久化在 <see cref="DivingFishAuthToken" /> 表中，
///     以 (qq, game) 直接定位，不依赖游戏绑定状态，也不做任何按 QQ 推算的 subject 探测。
/// </summary>
public static class DivingFishTokenStore
{
    /// <summary>同一 (qq, game) 的并发拉票去重。</summary>
    private static readonly ConcurrentDictionary<
        (long Qq, string Game),
        Lazy<Task<DivingFishToken?>>> InFlightFetches = new();

    public static async Task<DivingFishToken?> GetValidToken(long qq, string game)
    {
        game = NormalizeGame(game);

        var row = ReadRow(qq, game);
        if (row == null) return null;
        if (IsFresh(row)) return ToToken(row);

        var key = (qq, game);
        var fetch = InFlightFetches.GetOrAdd(key, _ =>
            new Lazy<Task<DivingFishToken?>>(
                () => MintAndStore(qq, game),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await fetch.Value;
        }
        finally
        {
            RemoveExact(InFlightFetches, key, fetch);
        }
    }

    /// <summary>
    ///     是否存有该 QQ 的授权：只查本地库，不发网络请求（票据新鲜与否由 <see cref="GetValidToken" /> 负责）。
    /// </summary>
    public static DivingFishToken? GetToken(long qq, string game)
    {
        game = NormalizeGame(game);

        var row = ReadRow(qq, game);
        return row == null ? null : ToToken(row);
    }

    /// <summary>设备码授权完成后写入（或覆盖）该 QQ 在指定游戏上的授权与票据。</summary>
    public static void SaveAuthorization(long qq, string game, string sub, DivingFishToken token)
    {
        game = NormalizeGame(game);
        DivingFishOAuth.SubjectForSub(sub); // 校验 sub 格式，非法直接拒绝

        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq, game) ?? realm.AddWithAutoId(new DivingFishAuthToken { Qq = qq, Game = game });
        row.Sub = sub;
        row.Scope = token.Scope;
        row.AccessToken = token.AccessToken;
        row.ExpiresAt = token.ExpiresAt;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        tr.Commit();
    }

    /// <summary>服务端拒绝当前票据（401）时调用：丢弃票据、保留授权，下次查询重新拉票。</summary>
    public static void RemoveToken(long qq, string game)
    {
        game = NormalizeGame(game);
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq, game);
        if (row != null)
        {
            row.AccessToken = "";
            row.ExpiresAt = default;
        }
        tr.Commit();
    }

    private static async Task<DivingFishToken?> MintAndStore(long qq, string game)
    {
        var row = ReadRow(qq, game);
        if (row == null) return null;
        if (IsFresh(row)) return ToToken(row); // 等待期间已被其它请求刷新

        string subject;
        try
        {
            subject = DivingFishOAuth.SubjectForSub(row.Sub);
        }
        catch (ArgumentException)
        {
            // 本地授权数据损坏：清掉，让用户重新绑定
            RemoveRow(qq, game);
            return null;
        }

        DivingFishToken token;
        try
        {
            token = await DivingFishOAuth.FetchToken(subject, game);
        }
        catch (DivingFishNotBoundException)
        {
            // 服务端已无该授权（被撤销）：删除本地授权，走重新绑定流程
            RemoveRow(qq, game);
            return null;
        }

        StoreTicket(qq, game, row.Sub, token);
        return token;
    }

    private static void StoreTicket(long qq, string game, string sub, DivingFishToken token)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq, game);
        // 拉票期间用户可能已换绑：sub 变了就不要把旧票写回去
        if (row != null && row.Sub.Equals(sub, StringComparison.Ordinal))
        {
            row.Scope = token.Scope;
            row.AccessToken = token.AccessToken;
            row.ExpiresAt = token.ExpiresAt;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        tr.Commit();
    }

    private static void RemoveRow(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        var row = Find(realm, qq, game);
        if (row != null) realm.Remove(row);
        tr.Commit();
    }

    private static RowSnapshot? ReadRow(long qq, string game)
    {
        using var realm = BotDbContext.OpenRealm();
        var row = Find(realm, qq, game);
        return row == null ? null : new RowSnapshot(row.Sub, row.Scope, row.AccessToken, row.ExpiresAt);
    }

    private static DivingFishAuthToken? Find(Realm realm, long qq, string game)
    {
        return realm.All<DivingFishAuthToken>().FirstOrDefault(x => x.Qq == qq && x.Game == game);
    }

    private static bool IsFresh(RowSnapshot row)
    {
        return !string.IsNullOrWhiteSpace(row.AccessToken) &&
               DateTime.UtcNow < row.ExpiresAt.AddSeconds(-30).UtcDateTime;
    }

    private static DivingFishToken ToToken(RowSnapshot row)
    {
        return new DivingFishToken
        {
            AccessToken = row.AccessToken,
            Scope = row.Scope,
            ExpiresAt = row.ExpiresAt.UtcDateTime
        };
    }

    private static void RemoveExact<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value) where TKey : notnull
    {
        ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, value));
    }

    private static string NormalizeGame(string game)
    {
        if (string.Equals(game, "maimai", StringComparison.OrdinalIgnoreCase)) return "maimai";
        if (string.Equals(game, "chunithm", StringComparison.OrdinalIgnoreCase)) return "chunithm";
        throw new ArgumentOutOfRangeException(nameof(game), game, "仅支持 maimai 或 chunithm");
    }

    private sealed record RowSnapshot(string Sub, string Scope, string AccessToken, DateTimeOffset ExpiresAt);
}
