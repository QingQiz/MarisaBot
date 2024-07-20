using System.Net;
using System.Net.Sockets;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private readonly Dictionary<string, DataFetcher> _dataFetchers = new();

    private DataFetcher GetDataFetcher(string name)
    {
        if (_dataFetchers.TryGetValue(name, out var fetcher)) return fetcher;

        try
        {
            return _dataFetchers[name] = name switch
            {
                "DivingFish" => new DivingFishDataFetcher(_songDb),
                "RinNET" => new AllNetBasedNetDataFetcher(_songDb, "aqua.naominet.live",
                    ConfigurationManager.Configuration.Chunithm.RinNetKeyChip),
                "Aqua" => new AllNetBasedNetDataFetcher(_songDb, "aqua.msm.moe",
                    ConfigurationManager.Configuration.Chunithm.AllNetKeyChip),
                _ => Dns.GetHostAddresses(name).Any()
                    ? new AllNetBasedNetDataFetcher(_songDb, name,
                        ConfigurationManager.Configuration.Chunithm.AllNetKeyChip)
                    : throw new InvalidDataException("无效的服务器名： " + name)
            };
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            throw new InvalidDataException("无效的服务器名： " + name);
        }
    }

    private static (string, int) LevelAlias2Index(ReadOnlyMemory<char> command, List<string> levels)
    {
        // 全名
        var level       = levels.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
        var levelPrefix = level ?? "";
        if (level != null) goto RightLabel;

        // 首字母
        level = levels.FirstOrDefault(n =>
            command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
        if (level != null)
        {
            levelPrefix = command.Span[0].ToString();
            goto RightLabel;
        }

        // 别名
        level = ChunithmSong.LevelAlias.Keys.FirstOrDefault(a =>
            command.StartsWith(a, StringComparison.OrdinalIgnoreCase));
        levelPrefix = level ?? "";

        if (level == null) return ("", -1);

        level = ChunithmSong.LevelAlias[level];

        RightLabel:
        return (levelPrefix, levels.IndexOf(level));
    }

    private async Task<DataFetcher> GetDataFetcher(Message message, bool allowUsername = false)
    {
        // Command不为空的话，就是用用户名查。只有DivingFish能使用用户名查
        if (allowUsername && !message.Command.IsWhiteSpace())
        {
            return GetDataFetcher("DivingFish");
        }

        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        await using var db = new BotDbContext();

        var bind = db.ChunithmBinds.FirstOrDefault(x => x.UId == qq);

        return GetDataFetcher(bind == null ? "DivingFish" : bind.ServerName);
    }

    private async Task<MessageChain> GetB30Card(Message message)
    {
        var fetcher = await GetDataFetcher(message, true);

        return MessageChain.FromImageB64((await fetcher.GetRating(message)).Draw().ToB64());
    }
}