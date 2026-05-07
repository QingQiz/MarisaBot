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
                "RinNET" => new AllNetBasedNetDataFetcher(SongDb, "RinNET", "aqua.naominet.live",
                    ConfigurationManager.Configuration.Chunithm.RinNetKeyChip, accessCode!),
                "Aqua" => new AllNetBasedNetDataFetcher(SongDb, "Aqua", "aqua.msm.moe",
                    ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode!),
                _ => Dns.GetHostAddresses(name).Length != 0
                    ? new AllNetBasedNetDataFetcher(SongDb, name, name,
                        ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode!)
                    : throw new InvalidDataException("无效的服务器名： " + name)
            };
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            throw new InvalidDataException("无效的服务器名： " + name);
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

        if (bind == null)
        {
            return GetDataFetcher("DivingFish", null); // 默认水鱼
        }

        var serverName = bind.ServerName;
        var accessCode = bind.AccessCode;

        return GetDataFetcher(serverName, accessCode);
    }

    private async Task<ChunithmRating> GetRating(Message message, bool b50 = false)
    {
        var fetcher = await GetDataFetcher(message, true);
        if (!b50) return await fetcher.GetRating(message);

        var rating = await fetcher.GetRating(message);
        var scores = await fetcher.GetScores(message);
        rating.IsB50 = true;

        var songList = fetcher.GetSongList();
        HashSet<string> newestVersions;

        if (fetcher is DivingFishDataFetcher)
        {
            newestVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CHUNITHM LUMINOUS PLUS",
                "CHUNITHM VERSE"
            };
        }
        else
        {
            newestVersions = songList
                .Select(s => s.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderDescending(StringComparer.OrdinalIgnoreCase)
                .Take(1)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var versionMap = songList.ToDictionary(s => s.Id, s => s.Version);

        var div = scores
            .Select(x => x.Value)
            .GroupBy(x => newestVersions.Contains(versionMap.GetValueOrDefault(x.Id, "")))
            .ToList();

        var r = div.FirstOrDefault(x => x.Key)?.Select(x => x) ?? [];
        var b = div.FirstOrDefault(x => !x.Key)?.Select(x => x) ?? [];
        r = r.OrderByDescending(x => x.Rating).Take(20);
        b = b.OrderByDescending(x => x.Rating).Take(30);

        rating.Records.Best   = b.ToArray();
        rating.Records.Recent = r.ToArray();
        return rating;
    }

    private async Task<MessageChain> GetRatingImg(Message message, bool b50 = false)
    {
        var ctx = new WebContext();
        ctx.Put("rating", await GetRating(message, b50));

        return MessageChain.FromImageB64(await WebApi.ChunithmBest(ctx.Id, b50));
    }
}
