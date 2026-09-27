using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Database.Entity.Plugin.MaiMaiDx;

namespace Marisa.Plugin.Shared.DivingFish;

public static class DivingFishBindingService
{
    /// <summary>
    ///     设备码授权确认后落库：写入水鱼授权与票据，并把该游戏的路由指向 DivingFish。
    /// </summary>
    public static void Commit(long qq, string sub, DivingFishToken token, string game)
    {
        if (string.IsNullOrWhiteSpace(sub)) throw new ArgumentException("sub is required", nameof(sub));

        var requiredScope = DivingFishOAuth.ScopeOf(game);
        var grantedScopes = token.Scope.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!grantedScopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            throw new ArgumentException("confirmation does not contain the required game scope", nameof(token));
        }

        DivingFishTokenStore.SaveAuthorization(qq, game, sub, token);

        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        switch (game)
        {
            case "maimai":
            {
                var binding = realm.All<MaiMaiDxBind>().FirstOrDefault(x => x.UId == qq);
                if (binding == null)
                {
                    realm.AddWithAutoId(new MaiMaiDxBind(qq, 0) { ServerName = "DivingFish" });
                }
                else
                {
                    binding.ServerName = "DivingFish";
                }
                break;
            }

            case "chunithm":
            {
                var binding = realm.All<ChunithmBind>().FirstOrDefault(x => x.UId == qq);
                if (binding == null)
                {
                    realm.AddWithAutoId(new ChunithmBind(qq, "DivingFish"));
                }
                else
                {
                    binding.ServerName = "DivingFish";
                    binding.AccessCode = "";
                }
                break;
            }
        }
        tr.Commit();
    }
}
