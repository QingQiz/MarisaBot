using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    #region 查分

    /// <summary>
    /// b30
    /// </summary>
    [MarisaPluginDoc("查询 b30，参数为：查分器的账号名 或 @某人 或 留空")]
    [MarisaPluginCommand("b30", "查分")]
    private static async Task<MarisaPluginTaskState> B30(Message message)
    {
        var ret = await GetB30Card(message);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("b30的汇总情况，具体的试一试命令就知道了（懒）")]
    [MarisaPluginSubCommand(nameof(B30))]
    [MarisaPluginCommand("sum")]
    private static async Task<MarisaPluginTaskState> B30Sum(Message message)
    {
        try
        {
            var (username, qq) = AtOrSelf(message);
            var rating = await GetRating(username, qq);

            var bSum = rating.Records.Best.Sum(x => x.Rating) * 100;
            var rSum = rating.Records.R10.Sum(x => x.Rating) * 100;

            message.Reply($"{rating.Username} ({rating.Rating})\nBest: {rating.B30}\nRecent: {rating.R10}\n\n" +
                $"推分剩余: 0.{40 - (bSum + rSum) % 40:00}\nBest 推分剩余: 0.{30 - bSum % 30:00}\nRecent 推分剩余: 0.{10 - rSum % 10:00}");
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            message.Reply("“查无此人”");
        }
        catch (FlurlHttpTimeoutException)
        {
            message.Reply("Timeout");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

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
    private async Task<MarisaPluginTaskState> ListSong(Message message)
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
    [MarisaPluginDoc("中二猜歌的排名，给出的结果中s,c,w分别是启动猜歌的次数，猜对的次数和猜错的次数")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(true, "排名")]
    private MarisaPluginTaskState GuessRank(Message message)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.ChunithmGuesses
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
    [MarisaPluginDoc("中二猜歌，看封面猜曲")]
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

    /// <summary>
    /// 分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginDoc("给出定数对应的一些 rating，参数为：歌曲定数")]
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState RatingLine(Message message)
    {
        if (decimal.TryParse(message.Command, out var constant))
        {
            if (constant is <= 16 and >= 1)
            {
                var a      = 97_4999;
                var lastRa = 0m;

                var ret = "达成率 -> Rating（每0.1分输出一次）";

                while (a < 100_9000)
                {
                    a = ChunithmSong.NextRa(a, constant);
                    var ra = ChunithmSong.Ra(a, constant);

                    if (ra - lastRa < 0.1m && a != 100_9000) continue;

                    lastRa = ra;
                    ret    = $"{ret}\n{a:0000000} -> {ra:00.00}";
                }

                message.Reply(ret);
                return MarisaPluginTaskState.CompletedTask;
            }
        }

        message.Reply("参数应为“定数”");
        return MarisaPluginTaskState.CompletedTask;
    }

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

            var levelName = song.LevelName;
            var level     = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));

            if (level == null)
            {
                next.Reply("错误的难度名，会话已关闭。可用的难度名：" + string.Join("、", levelName));
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var levelIdx = levelName.IndexOf(level);

            if (song.MaxCombo[levelIdx] == 0)
            {
                next.Reply("暂无该难度的数据");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var parseSuccess = int.TryParse(command.TrimStart(level), out var achievement);

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

            var maxCombo  = song.MaxCombo[levelIdx];
            var tolerance = 101_0000 - achievement;
            var noteScore = 101_0000.0 / maxCombo;

            var greenScore = 50.0 / 101 * noteScore;
            var 小p减分       = 1.0 / 101 * noteScore;

            var greenCount = (int)(tolerance / (noteScore - greenScore));
            var grayCount  = (int)(tolerance / noteScore);
            var greenRest  = tolerance - greenCount * greenScore;
            var grayRest   = tolerance - grayCount * noteScore;

            next.Reply(
                new MessageDataText($"[{levelName[levelIdx]}] {song.Title} => {achievement}\n"),
                new MessageDataText($"至多绿 {greenCount} 个 + {(int)(greenRest / 小p减分)} 小\n"),
                new MessageDataText($"至多灰 {grayCount} 个 + {(int)(grayRest / 小p减分)} 小\n"),
                new MessageDataText($"每个绿减 {noteScore - greenScore:F2}，每个灰减 {noteScore:F2}，每小减 {小p减分:F2}")
            );

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}