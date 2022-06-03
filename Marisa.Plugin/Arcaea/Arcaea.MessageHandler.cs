using Marisa.EntityFrameworkCore;

namespace Marisa.Plugin.Arcaea;

[MarisaPlugin(PluginPriority.Arcaea)]
[MarisaPluginDoc("音游 Arcaea 相关功能，命令前缀：arcaea、arc、阿卡伊")]
[MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public partial class Arcaea : MarisaPluginBase
{
    /// <summary>
    /// 搜歌
    /// </summary>
    [MarisaPluginDoc("搜歌，命令为：song、search、搜索 + 歌名")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MarisaPluginTaskState ArcaeaSearchSong(Message message)
    {
        var search = _songDb.SearchSong(message.Command);

        message.Reply(_songDb.GetSearchResult(search));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 猜歌排名
    /// </summary>
    [MarisaPluginDoc("猜歌排名，命令为：排名")]
    [MarisaPluginSubCommand(nameof(ArcaeaGuess))]
    [MarisaPluginCommand(true, "排名")]
    private MarisaPluginTaskState ArcaeaGuessRank(Message message)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.ArcaeaGuesses
            .OrderByDescending(g => g.TimesCorrect)
            .ThenBy(g => g.TimesWrong)
            .ThenBy(g => g.TimesStart)
            .Take(10)
            .ToList();

        if (!res.Any()) message.Reply("None");

        message.Reply(string.Join('\n', res.Select((guess, i) =>
            $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})")));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 猜歌
    /// </summary>
    [MarisaPluginDoc("猜歌，命令为：猜歌、猜曲、guess")]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MarisaPluginTaskState ArcaeaGuess(Message message, long qq)
    {
        if (string.IsNullOrEmpty(message.Command))
        {
            _songDb.StartSongCoverGuess(message, qq, 4, null);
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 听歌猜曲
    /// </summary>
    [MarisaPluginDoc("听歌猜曲，命令为：v2")]
    [MarisaPluginSubCommand(nameof(ArcaeaGuess))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, true, "v2")]
    private MarisaPluginTaskState ArcaeaGuessV2(Message message, long qq)
    {
        StartSongSoundGuess(message, qq);
        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 别名处理
    /// </summary>
    [MarisaPluginDoc("别名设置和查询，命令为：alias")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private static MarisaPluginTaskState ArcaeaSongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MarisaPluginDoc("获取别名，命令为：get + 歌名/别名")]
    [MarisaPluginSubCommand(nameof(ArcaeaSongAlias))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "get")]
    private MarisaPluginTaskState ArcaeaSongAliasGet(Message message)
    {
        var songName = message.Command;

        if (string.IsNullOrEmpty(songName))
        {
            message.Reply("？");
        }

        var songList = _songDb.SearchSong(songName);

        if (songList.Count == 1)
        {
            message.Reply($"当前歌在录的别名有：{string.Join('、', _songDb.GetSongAliasesByName(songList[0].Title))}");
        }
        else
        {
            message.Reply(_songDb.GetSearchResult(songList));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 设置别名
    /// </summary>
    [MarisaPluginDoc("设置别名，命令为：set 歌曲原名 := 歌曲别名")]
    [MarisaPluginSubCommand(nameof(ArcaeaSongAlias))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "set")]
    private MarisaPluginTaskState ArcaeaSongAliasSet(Message message)
    {
        var param = message.Command;
        var names = param.Split(":=");

        if (names.Length != 2)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var name  = names[0].Trim();
        var alias = names[1].Trim();

        message.Reply(_songDb.SetSongAlias(name, alias) ? "Success" : $"不存在的歌曲：{name}");

        return MarisaPluginTaskState.CompletedTask;
    }
}