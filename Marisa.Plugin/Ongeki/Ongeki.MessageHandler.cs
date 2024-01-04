using System.Diagnostics.CodeAnalysis;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.Ongeki;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Ongeki;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Ongeki
{
    #region 搜歌

    /// <summary>
    /// 搜歌
    /// </summary>
    [MarisaPluginDoc("搜歌，参数为：歌曲名 或 歌曲别名 或 歌曲id")]
    [MarisaPluginCommand("song", "search", "搜索")]
    private MarisaPluginTaskState SearchSong(Message message)
    {
        return _songDb.SearchSong(message);
    }

    #endregion

    #region 歌曲别名相关

    /// <summary>
    /// 别名处理
    /// </summary>
    [MarisaPluginDoc("别名设置和查询")]
    [MarisaPluginCommand("alias")]
    private static MarisaPluginTaskState SongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MarisaPluginDoc("获取别名，参数为：歌名/别名")]
    [MarisaPluginSubCommand(nameof(SongAlias))]
    [MarisaPluginCommand("get")]
    private MarisaPluginTaskState SongAliasGet(Message message)
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
    [MarisaPluginSubCommand(nameof(SongAlias))]
    [MarisaPluginCommand("set")]
    private MarisaPluginTaskState SongAliasSet(Message message)
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

    #endregion

    #region 筛选

    /// <summary>
    /// 给出歌曲列表
    /// </summary>
    [MarisaPluginDoc("给出符合条件的歌曲，结果过多时回复 p1、p2 等获取额外的信息")]
    [MarisaPluginCommand("list", "ls")]
    private static async Task<MarisaPluginTaskState> ListSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginTrigger(typeof(MaiMaiDx.MaiMaiDx), nameof(MaiMaiDx.MaiMaiDx.ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    private Task<MarisaPluginTaskState> ListSongBase(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByBaseRange(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("charter", "谱师")]
    private Task<MarisaPluginTaskState> ListSongCharter(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByCharter(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    private Task<MarisaPluginTaskState> ListSongLevel(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByLevel(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("bpm")]
    private Task<MarisaPluginTaskState> ListSongBpm(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByBpmRange(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("artist", "a")]
    private Task<MarisaPluginTaskState> ListSongArtist(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByArtist(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 随机

    /// <summary>
    /// 随机
    /// </summary>
    [MarisaPluginDoc("随机给出一个符合条件的歌曲")]
    [MarisaPluginCommand("random", "rand", "随机")]
    private async Task<MarisaPluginTaskState> RandomSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginTrigger(typeof(MaiMaiDx.MaiMaiDx), nameof(MaiMaiDx.MaiMaiDx.ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    private Task<MarisaPluginTaskState> RandomSongBase(Message message)
    {
        _songDb.SelectSongByBaseRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("charter", "谱师")]
    private Task<MarisaPluginTaskState> RandomSongCharter(Message message)
    {
        _songDb.SelectSongByCharter(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    private Task<MarisaPluginTaskState> RandomSongLevel(Message message)
    {
        _songDb.SelectSongByLevel(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("bpm")]
    private Task<MarisaPluginTaskState> RandomSongBpm(Message message)
    {
        _songDb.SelectSongByBpmRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("artist", "a")]
    private Task<MarisaPluginTaskState> RandomSongArtist(Message message)
    {
        _songDb.SelectSongByArtist(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 猜曲

    /// <summary>
    /// 猜歌排名
    /// </summary>
    [MarisaPluginDoc("音击猜歌的排名，给出的结果中s,c,w分别是启动猜歌的次数，猜对的次数和猜错的次数")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(true, "排名")]
    private MarisaPluginTaskState GuessRank(Message message)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.OngekiGuesses
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
    [MarisaPluginDoc("音击猜歌，看封面猜曲")]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MarisaPluginTaskState Guess(Message message, long qq)
    {
        if (message.Command == "")
        {
            _songDb.StartSongCoverGuess(message, qq, 3, null);
        }
        else
        {
            message.Reply("错误的命令格式");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 分数线 / 容错率

    [MarisaPluginDoc("计算某首歌曲的容错率，参数为：歌名")]
    [MarisaPluginCommand("tolerance", "容错率")]
    private MarisaPluginTaskState FaultTolerance(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = _songDb.SearchSong(songName);

        if (searchResult.Count != 1)
        {
            message.Reply(_songDb.GetSearchResult(searchResult));
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("难度和预期达成率？");
        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, next =>
        {
            var command = next.Command.Trim();
            var song    = searchResult.First();

            var levelName = OngekiSong.LevelAlias.Values.ToList();

            // 全名
            var level       = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            var levelPrefix = level ?? "";
            if (level != null) goto RightLabel;

            // 首字母
            level = levelName.FirstOrDefault(n =>
                command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
            if (level != null)
            {
                levelPrefix = command[0].ToString();
                goto RightLabel;
            }

            // 别名
            level = OngekiSong.LevelAlias.Keys.FirstOrDefault(a =>
                command.StartsWith(a, StringComparison.OrdinalIgnoreCase));
            levelPrefix = level ?? "";
            if (level != null)
            {
                level = OngekiSong.LevelAlias[level];
                goto RightLabel;
            }

            next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、难度全名的首字母或难度颜色");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);

            RightLabel:
            var levelIdx = levelName.IndexOf(level);

            if (levelIdx > song.Charts.Count - 1      ||
                song.Charts[levelIdx] is null         ||
                song.Charts[levelIdx]!.NoteCount == 0 ||
                song.Charts[levelIdx]!.BellCount == 0)
            {
                next.Reply("暂无该难度的数据");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var parseSuccess = int.TryParse(command.TrimStart(levelPrefix), out var achievement);

            if (!parseSuccess)
            {
                next.Reply("错误的达成率格式，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (achievement is > 101_0000 or < 0)
            {
                next.Reply("你查**呢");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var noteScore = 95_0000m / song.Charts[levelIdx]!.NoteCount;
            var bellScore = 6_0000m  / song.Charts[levelIdx]!.BellCount;

            var @break = 0.1m * noteScore;
            var hit = 0.4m * noteScore;
            
            var tolerance = 101_0000 - achievement;
            
            next.Reply(
                new MessageDataText($"[{levelName[levelIdx]}] {song.Title} => {achievement}\n"),
                new MessageDataText($"至多有 {tolerance / @break:F1} 个 break，每个 break 减 {@break:F1} 分\n"),
                new MessageDataText($"至多有 {tolerance / hit:F1} 个 hit，每个 hit 减 {hit:F1} 分\n"),
                new MessageDataText($"至多有 {tolerance / noteScore:F1} 个 miss，每个 miss 减 {noteScore:F1} 分\n\n"),
                new MessageDataText($"每个 bell 有 {bellScore:F1} 分\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / @break:F1} break\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / hit:F1} hit\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / noteScore:F1} miss\n")
            );

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}