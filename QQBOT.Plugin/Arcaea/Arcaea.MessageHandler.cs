using QQBot.EntityFrameworkCore;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Arcaea;

namespace QQBot.Plugin.Arcaea;

[MiraiPlugin(19)]
[MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public partial class Arcaea : MiraiPluginBase
{
    /// <summary>
    /// 搜歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MiraiPluginTaskState ArcaeaSearchSong(Message message, MessageSenderProvider ms)
    {
        var search = SearchSongByAlias(message.Command);

        ms.SendByRecv(GetSearchResult(search), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 猜歌排名
    /// </summary>
    [MiraiPluginSubCommand(nameof(ArcaeaGuess))]
    [MiraiPluginCommand(true, "排名")]
    private MiraiPluginTaskState ArcaeaGuessRank(Message message, MessageSenderProvider ms)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.ArcaeaGuesses
            .OrderByDescending(g => g.TimesCorrect)
            .ThenBy(g => g.TimesWrong)
            .ThenBy(g => g.TimesStart)
            .Take(10)
            .ToList();

        if (!res.Any()) ms.Reply("None", message);

        ms.Reply(string.Join('\n', res.Select((guess, i) =>
                $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})")),
            message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 猜歌
    /// </summary>
    [MiraiPluginCommand(MiraiMessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MiraiPluginTaskState ArcaeaGuess(Message message, MessageSenderProvider ms, long qq)
    {
        StartSongCoverGuess(message, ms, qq);
        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 别名处理
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private static MiraiPluginTaskState ArcaeaSongAlias(Message message, MessageSenderProvider ms)
    {
        ms.Reply("错误的命令格式", message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MiraiPluginSubCommand(nameof(ArcaeaSongAlias))]
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "get")]
    private MiraiPluginTaskState ArcaeaSongAliasGet(Message message, MessageSenderProvider ms)
    {
        var songName = message.Command;

        if (string.IsNullOrEmpty(songName))
        {
            ms.Reply("？", message);
        }

        var songList = SearchSongByAlias(songName);

        if (songList.Count == 1)
        {
            ms.Reply($"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(songList[0].Title))}", message);
        }
        else
        {
            ms.Reply(GetSearchResult(songList), message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 设置别名
    /// </summary>
    [MiraiPluginSubCommand(nameof(ArcaeaSongAlias))]
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "set")]
    private MiraiPluginTaskState ArcaeaSongAliasSet(Message message, MessageSenderProvider ms)
    {
        var param = message.Command;
        var names = param.Split("$>");

        if (names.Length != 2)
        {
            ms.Reply("错误的命令格式", message);
            return MiraiPluginTaskState.CompletedTask;
        }

        lock (SongAlias)
        {
            var name  = names[0].Trim();
            var alias = names[1].Trim();

            if (SongList.Any(song => song.Title == name))
            {
                File.AppendAllText(
                    ResourceManager.TempPath + "/ArcaeaSongAliasTemp.txt", $"{name}\t{alias}\n");

                if (SongAlias.ContainsKey(alias))
                    SongAlias[alias].Add(name);
                else
                    SongAlias[alias] = new List<string> { name };

                ms.Reply("Success", message);
            }
            else
            {
                ms.Reply($"不存在的歌曲：{name}", message);
            }
        }

        return MiraiPluginTaskState.CompletedTask;
    }
}