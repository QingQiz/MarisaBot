using System.Text.RegularExpressions;
using Flurl.Http;
using QQBot.EntityFrameworkCore;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.MiraiHttp.Util;
using QQBot.Plugin.Shared;
using QQBot.Plugin.Shared.MaiMaiDx;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.MaiMaiDx;

[MiraiPlugin(PluginPriority.MaiMaiDx)]
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
        var ret = await GetB40Card(message, false);

        ms.Reply(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// b50
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "b50")]
    private static async Task<MiraiPluginTaskState> MaiMaiDxB50(Message message, MessageSenderProvider ms)
    {
        var ret = await GetB40Card(message, true);

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

    /// <summary>
    /// mai什么
    /// </summary>
    [MiraiPluginCommand("打什么歌", "打什么", "什么")]
    private MiraiPluginTaskState MaiMaiDxPlayWhat(Message message, MessageSenderProvider ms)
    {
        ms.Reply(MessageChain.FromImageB64(_songDb.SongList.RandomTake().GetImage()), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// mai什么推分
    /// </summary>
    [MiraiPluginSubCommand(nameof(MaiMaiDxPlayWhat))]
    [MiraiPluginCommand(true, "推分", "恰分", "上分", "加分")]
    private async Task<MiraiPluginTaskState> MaiMaiDxPlayWhatToUp(Message message, MessageSenderProvider ms)
    {
        var sender   = message.Sender!.Id;

        // 拿b40
        try
        {
            var rating    = await GetDxRating(null, sender);
            var recommend = rating.GetRecommendCards(_songDb.SongList);

            if (recommend == null)
            {
                ms.Reply("您无分可恰", message);
            }
            else
            {
                ms.Reply(ImageMessage.FromBase64(recommend.ToB64()), message);
            }
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            ms.Reply(MessageChain.FromPlainText("你谁啊？"), message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion

    #region list

    /// <summary>
    /// 给出歌曲列表
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "list", "ls")]
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
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, true, "v2")]
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

    #region Line / 分数线

    /// <summary>
    /// 分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MiraiPluginCommand("line", "分数线")]
    private static MiraiPluginTaskState MaiMaiDxSongLine(Message message, MessageSenderProvider ms)
    {
        if (double.TryParse(message.Command, out var constant))
        {
            if (constant <= 15.0)
            {
                var a   = 96.9999;
                var ret = "达成率 -> Rating";

                while (a < 100.5)
                {
                    a = SongScore.NextRa(a, constant);
                    var ra = SongScore.Ra(a, constant);
                    ret = $"{ret}\n{a:000.0000} -> {ra}";
                }

                ms.Reply(ret, message);
                return MiraiPluginTaskState.CompletedTask;
            }
        }

        ms.Reply("参数应为“定数”", message);
        return MiraiPluginTaskState.CompletedTask;
    }

    #endregion
}