﻿using System.Net;
using System.Net.Sockets;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private DataFetcher GetDataFetcher(string name, ChunithmBind? bind)
    {
        try
        {
            return name switch
            {
                "DivingFish" => new DivingFishDataFetcher(SongDb),
                "Louis"      => new LouisDataFetcher(SongDb),
                "RinNET" => new AllNetBasedNetDataFetcher(SongDb, "aqua.naominet.live",
                    ConfigurationManager.Configuration.Chunithm.RinNetKeyChip, bind!),
                "Aqua" => new AllNetBasedNetDataFetcher(SongDb, "aqua.msm.moe",
                    ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, bind!),
                _ => Dns.GetHostAddresses(name).Length != 0
                    ? new AllNetBasedNetDataFetcher(SongDb, name,
                        ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, bind!)
                    : throw new InvalidDataException("无效的服务器名： " + name)
            };
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            throw new InvalidDataException("无效的服务器名： " + name);
        }
    }

    private static (string, int) LevelAlias2Index(ReadOnlyMemory<char> command, List<string> diffs)
    {
        // 全名
        var level       = diffs.FirstOrDefault(d => command.StartsWith(d, StringComparison.OrdinalIgnoreCase));
        var levelPrefix = level ?? "";
        if (level != null) goto RightLabel;

        // 首字母
        level = diffs.FirstOrDefault(n =>
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
        return (levelPrefix, diffs.IndexOf(level));
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

        await using var db = new BotDbContext();

        var bind = db.ChunithmBinds.FirstOrDefault(x => x.UId == qq);

        return bind == null
            ? GetDataFetcher("DivingFish", null) // 默认水鱼
            : GetDataFetcher(bind.ServerName, bind);
    }

    private async Task<MessageChain> GetB30Card(Message message)
    {
        var fetcher = await GetDataFetcher(message, true);

        return MessageChain.FromImageB64((await fetcher.GetRating(message)).Draw().ToB64());
    }
}