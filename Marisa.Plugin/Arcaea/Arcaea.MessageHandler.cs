using Marisa.EntityFrameworkCore;

namespace Marisa.Plugin.Arcaea;

[MarisaPlugin(PluginPriority.Arcaea)]
[MarisaPluginDoc("音游 Arcaea 相关功能")]
[MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public partial class Arcaea : PluginBase
{
    /// <summary>
    /// 搜歌
    /// </summary>
    [MarisaPluginDoc("搜歌，参数为：歌名")]
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
    [MarisaPluginDoc("Arcaea 猜歌的排名，给出的结果中s,c,w分别是启动猜歌的次数，猜对的次数和猜错的次数")]
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
    [MarisaPluginDoc("Arcaea 猜歌，看封面猜曲")]
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
    [MarisaPluginDoc("Arcaea 猜歌，听歌猜曲")]
    [MarisaPluginSubCommand(nameof(ArcaeaGuess))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, true, "v2")]
    private MarisaPluginTaskState ArcaeaGuessV2(Message message, long qq)
    {
        message.Reply("死了的功能");
        // StartSongSoundGuess(message, qq);
        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 别名处理
    /// </summary>
    [MarisaPluginDoc("别名设置和查询")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private static MarisaPluginTaskState ArcaeaSongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MarisaPluginDoc("获取别名，参数为：歌名/别名")]
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
    [MarisaPluginDoc("设置别名，参数为：歌曲原名 或 歌曲id := 歌曲别名")]
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