using System.Text.RegularExpressions;
using Flurl.Http;
using QQBot.EntityFrameworkCore;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.MiraiHttp.Util;
using QQBot.Plugin.Shared.MaiMaiDx;

namespace QQBot.Plugin.MaiMaiDx;

[MiraiPlugin(20)]
[MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "maimai", "mai", "舞萌")]
public partial class MaiMaiDx : MiraiPluginBase
{
    #region b40

    /// <summary>
    /// b40
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "b40", "查分")]
    private static async Task<MiraiPluginTaskState> MaiMaiDxB40(Message message, MessageSenderProvider ms)
    {
        var username = message.Command;
        var sender   = message.Sender!.Id;
        var ret      = await GetB40Card(username, sender, false);

        ms.Reply(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// b50
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "b50")]
    private static async Task<MiraiPluginTaskState> MaiMaiDxB50(Message message, MessageSenderProvider ms)
    {
        var username = message.Command;
        var sender   = message.Sender!.Id;
        var ret      = await GetB40Card(username, sender, true);

        ms.Reply(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region search

    /// <summary>
    /// 搜歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MiraiPluginTaskState MaiMaiDxSearchSong(Message message, MessageSenderProvider ms)
    {
        var search = _songDb.SearchSong(message.Command);

        ms.Reply(_songDb.GetSearchResult(search), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region random

    /// <summary>
    /// 随机给出一个歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "random", "随机", "rand")]
    private MiraiPluginTaskState MaiMaiDxRandomSong(Message message, MessageSenderProvider ms)
    {
        var list = ListSongs(message.Command);

        ms.Reply(list.Count == 0
            ? MessageChain.FromPlainText("“NULL”")
            : MessageChain.FromImageB64(list[new Random().Next(list.Count)].GetImage()), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region list

    /// <summary>
    /// 给出歌曲列表
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "list")]
    private MiraiPluginTaskState MaiMaiDxListSong(Message message, MessageSenderProvider ms)
    {
        var    list = ListSongs(message.Command);
        string ret;

        if (list.Count == 0)
        {
            ret = "“EMPTY”";
        }
        else
        {
            var rand = new Random();
            ret = string.Join('\n',
                list.OrderBy(_ => rand.Next())
                    .Take(15)
                    .OrderBy(x => x.Id)
                    .Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}"));

            if (list.Count > 15) ret += "\n" + $"太多了（{list.Count}），随机给出15个";
        }

        ms.Reply(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region guess

    /// <summary>
    /// 舞萌猜歌排名
    /// </summary>
    [MiraiPluginSubCommand(nameof(MaiMaiDxGuess))]
    [MiraiPluginCommand(true, "排名")]
    private MiraiPluginTaskState MaiMaiDxGuessRank(Message message, MessageSenderProvider ms)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.MaiMaiDxGuesses
            .OrderByDescending(g => g.TimesCorrect)
            .ThenBy(g => g.TimesWrong)
            .ThenBy(g => g.TimesStart)
            .Take(10)
            .ToList();

        if (!res.Any()) ms.Reply(MessageChain.FromPlainText("None"), message);

        ms.Reply(string.Join('\n', res.Select((guess, i) =>
                $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})")),
            message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 听歌猜曲
    /// </summary>
    [MiraiPluginSubCommand(nameof(MaiMaiDxGuess))]
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "v2")]
    private MiraiPluginTaskState MaiMaiDxGuessV2(Message message, MessageSenderProvider ms, long qq)
    {
        StartSongSoundGuess(message, ms, qq);
        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 舞萌猜歌
    /// </summary>
    [MiraiPluginCommand(MiraiMessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MiraiPluginTaskState MaiMaiDxGuess(Message message, MessageSenderProvider ms, long qq)
    {
        if (message.Command.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
        {
            var reg = message.Command[2..];

            if (!reg.IsRegex())
            {
                ms.Reply("错误的正则表达式：" + reg, message);
            }
            else
            {
                _songDb.StartSongCoverGuess(message, ms, qq, 3, song =>
                    new Regex(reg, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)
                        .IsMatch(song.Info.Genre));
            }
        }
        else if (message.Command == "")
        {
            _songDb.StartSongCoverGuess(message, ms, qq, 3, null);
        }
        else
        {
            ms.Reply("错误的命令格式", message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region Alias

    /// <summary>
    /// 别名处理
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private static MiraiPluginTaskState MaiMaiDxSongAlias(Message message, MessageSenderProvider ms)
    {
        ms.Reply("错误的命令格式", message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MiraiPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "get")]
    private MiraiPluginTaskState MaiMaiDxSongAliasGet(Message message, MessageSenderProvider ms)
    {
        var songName = message.Command;

        if (string.IsNullOrEmpty(songName))
        {
            ms.Reply("？", message);
        }

        var songList = _songDb.SearchSong(songName);

        if (songList.Count == 1)
        {
            ms.Reply($"当前歌在录的别名有：{string.Join('、', _songDb.GetSongAliasesByName(songList[0].Title))}", message);
        }
        else
        {
            ms.Reply(_songDb.GetSearchResult(songList), message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 设置别名
    /// </summary>
    [MiraiPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "set")]
    private MiraiPluginTaskState MaiMaiDxSongAliasSet(Message message, MessageSenderProvider ms)
    {
        var param = message.Command;
        var names = param.Split("$>");

        if (names.Length != 2)
        {
            ms.Reply("错误的命令格式", message);
            return MiraiPluginTaskState.CompletedTask;
        }

        var name  = names[0].Trim();
        var alias = names[1].Trim();

        ms.Reply(_songDb.SetSongAlias(name, alias) ? "Success" : $"不存在的歌曲：{name}", message);

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion
}