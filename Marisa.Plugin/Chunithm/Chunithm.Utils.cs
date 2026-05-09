using System.Net;
using System.Net.Sockets;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private DataFetcher GetDataFetcher(string name, string? accessCode)
    {
        try
        {
            return name switch
            {
                "DivingFish" => new DivingFishDataFetcher(SongDb),
                "Louis"      => new LouisDataFetcher(SongDb),
                "lxns"       => new LxnsDataFetcher(SongDb),
                "RinNET" => new AllNetBasedNetDataFetcher(SongDb, "RinNET", "aqua.naominet.live",
                    ConfigurationManager.Configuration.Chunithm.RinNetKeyChip, accessCode),
                "Aqua" => new AllNetBasedNetDataFetcher(SongDb, "Aqua", "aqua.msm.moe",
                    ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode),
                _ => Dns.GetHostAddresses(name).Length != 0
                    ? new AllNetBasedNetDataFetcher(SongDb, name, name,
                        ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode)
                    : throw new InvalidDataException("无效的服务器名：" + name)
            };
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            throw new InvalidDataException("无效的服务器名：" + name);
        }
    }

    private async Task<DataFetcher> GetDataFetcher(Message message, bool allowUsername = false)
    {
        // Command不为空的话，就是用用户名查。只有DivingFish能使用用户名查。
        // NOTE Louis也能用用户名查，但现在还是默认水鱼吧
        if (allowUsername && !message.Command.IsWhiteSpace())
        {
            return GetDataFetcher("DivingFish", null);
        }

        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<ChunithmBind>().FirstOrDefault(x => x.UId == qq);

        return bind == null
            ? GetDataFetcher("DivingFish", null)
            : GetDataFetcher(bind.ServerName, bind.AccessCode);
    }

    private async Task<ChunithmRating> GetRating(Message message, bool b50 = false)
    {
        var fetcher = await GetDataFetcher(message, true);
        
        // 如果是 lxns 查分器
        if (fetcher is LxnsDataFetcher lxnsFetcher)
        {
            // B50时返回原始数据（best对应best，new_best对应recent）
            if (b50)
            {
                var rating = await lxnsFetcher.GetRatingRaw(message);
                rating.IsB50 = true;
                return rating;
            }
            // B30时返回合并后的数据（旧版本逻辑）
            else
            {
                var rating = await lxnsFetcher.GetRating(message);
                rating.IsB50 = false;
                return rating;
            }
        }

        var baseRating = await fetcher.GetRating(message);
        
        if (!b50) return baseRating;

        var scores = await fetcher.GetScores(message);
        var newestVersions = new[] { "CHUNITHM VERSE", "CHUNITHM LUMINOUS PLUS" };

        baseRating.IsB50 = true;

        var div = scores.Select(s => s.Value).GroupBy(s => 
        {
            var songObj = SongDb.GetSongById(s.Id);
            // 如果找不到歌曲，默认分到旧版本组
            return songObj != null && newestVersions.Contains(songObj.Version);
        }).ToList();

        var r = div.FirstOrDefault(g => g.Key)?.ToList() ?? [];
        var b = div.FirstOrDefault(g => !g.Key)?.ToList() ?? [];
        r = r.OrderByDescending(s => s.Rating).Take(20).ToList();
        b = b.OrderByDescending(s => s.Rating).Take(30).ToList();

        baseRating.Records.Best   = b.ToArray();
        baseRating.Records.Recent = r.ToArray();
        return baseRating;
    }

    private async Task<MessageChain> GetRatingImg(Message message, bool b50 = false)
    {
        var ctx = new WebContext();
        ctx.Put("rating", await GetRating(message, b50));

        return MessageChain.FromImageB64(await WebApi.ChunithmBest(ctx.Id, b50));
    }
}