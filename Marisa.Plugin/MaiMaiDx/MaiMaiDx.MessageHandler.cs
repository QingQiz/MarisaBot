using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

[MarisaPlugin(PluginPriority.MaiMaiDx)]
[MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "maimai", "mai", "舞萌")]
public partial class MaiMaiDx : MarisaPluginBase
{
    #region Summary

    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "summary")]
    private static async Task<MarisaPluginTaskState> MaiMaiSummary(Message message)
    {
        message.Reply("错误的命令格式");

        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "base", "b")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryBase(Message message)
    {
        var constants = message.Command.Split('-').Select(x =>
        {
            var res = double.TryParse(x.Trim(), out var c);
            return res ? c : -1;
        }).ToList();

        if (constants.Count != 2 || constants.Any(c => c < 0))
        {
            message.Reply("错误的命令格式");
        }
        else
        {
            // 太大的话画图会失败，所以给判断一下
            if (constants[1] - constants[0] > 3)
            {
                message.Reply("过大的跨度");
                return MarisaPluginTaskState.CompletedTask;
            }

            var scores = await GetAllSongScores(message);
            if (scores == null)
            {
                return MarisaPluginTaskState.NoResponse;
            }

            var groupedSong = _songDb.SongList
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(x => x.constant >= constants[0] && x.constant <= constants[1])
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.constant.ToString("F1"));

            var im = await Task.Run(() => DrawGroupedSong(groupedSong, scores));

            if (im == null)
            {
                message.Reply("EMPTY");
            }
            else
            {
                message.Reply(MessageDataImage.FromBase64(im.ToB64()));
            }
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "genre", "type")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryGenre(Message message)
    {
        var genre = MaiMaiSong.Genres.FirstOrDefault(p =>
            string.Equals(p, message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (genre == null)
        {
            message.Reply("可用的类别有：\n" + string.Join('\n', MaiMaiSong.Genres));
        }
        else
        {
            var scores = await GetAllSongScores(message);
            if (scores == null)
            {
                return MarisaPluginTaskState.NoResponse;
            }

            var groupedSong = _songDb.SongList
                .Where(song => song.Info.Genre == genre)
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(data => data.i >= 2)
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.song.Levels[x.i]);

            var im = await Task.Run(() => DrawGroupedSong(groupedSong, scores));

            // 不可能是 null
            message.Reply(MessageDataImage.FromBase64(im!.ToB64()));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "version", "ver")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryVersion(Message message)
    {
        var version = MaiMaiSong.Plates.FirstOrDefault(p =>
            string.Equals(p, message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (version == null)
        {
            message.Reply("可用的版本号有：\n" + string.Join('\n', MaiMaiSong.Plates));
        }
        else
        {
            var scores = await GetAllSongScores(message, new[] { version });
            if (scores == null)
            {
                return MarisaPluginTaskState.NoResponse;
            }

            var groupedSong = _songDb.SongList
                .Where(song => song.Version == version)
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(data => data.i >= 2)
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.song.Levels[x.i]);

            var im = await Task.Run(() => DrawGroupedSong(groupedSong, scores));

            // 不可能是 null
            message.Reply(MessageDataImage.FromBase64(im!.ToB64()));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "level", "lv")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryLevel(Message message)
    {
        var lv = message.Command.Trim();

        if (new Regex(@"^[0-9]+\+?$").IsMatch(lv))
        {
            var maxLv = lv.EndsWith('+') ? 14 : 15;
            var lvNr  = lv.EndsWith('+') ? lv[..^1] : lv;

            if (int.TryParse(lvNr, out var i))
            {
                if (!(1 <= i && i <= maxLv))
                {
                    goto _error;
                }
            }
            else
            {
                goto _error;
            }
        }
        else
        {
            goto _error;
        }

        var scores = await GetAllSongScores(message);
        if (scores == null)
        {
            return MarisaPluginTaskState.NoResponse;
        }

        var groupedSong = _songDb.SongList
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i] == lv)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await Task.Run(() => DrawGroupedSong(groupedSong, scores));

        // 不可能是 null
        message.Reply(MessageDataImage.FromBase64(im!.ToB64()));

        return MarisaPluginTaskState.CompletedTask;

        // 集中处理错误
        _error:
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 查分

    /// <summary>
    /// b40
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "b40", "查分")]
    private static async Task<MarisaPluginTaskState> MaiMaiDxB40(Message message)
    {
        var ret = await GetB40Card(message, false);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// b50
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "b50")]
    private static async Task<MarisaPluginTaskState> MaiMaiDxB50(Message message)
    {
        var ret = await GetB40Card(message, true);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 搜歌

    /// <summary>
    /// 搜歌
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MarisaPluginTaskState MaiMaiDxSearchSong(Message message)
    {
        var search = _songDb.SearchSong(message.Command);

        message.Reply(_songDb.GetSearchResult(search));

        if (search.Count is > 1 and < SongDbConfig.PageSize)
        {
            Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, hMessage =>
            {
                // 不是 id
                if (!long.TryParse(hMessage.Command.Trim(), out var songId))
                {
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                var song = _songDb.GetSongById(songId);
                // 没找到歌
                if (song == null)
                {
                    return Task.FromResult(MarisaPluginTaskState.Canceled);
                }

                message.Reply(_songDb.GetSearchResult(new [] { song }));
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            });
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 打什么歌

    /// <summary>
    /// 随机给出一个歌
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "random", "随机", "rand")]
    private MarisaPluginTaskState MaiMaiDxRandomSong(Message message)
    {
        var list = ListSongs(message.Command);

        message.Reply(list.Count == 0
            ? MessageChain.FromText("“NULL”")
            : MessageChain.FromImageB64(list[new Random().Next(list.Count)].GetImage()));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// mai什么
    /// </summary>
    [MarisaPluginCommand("打什么歌", "打什么", "什么")]
    private MarisaPluginTaskState MaiMaiDxPlayWhat(Message message)
    {
        message.Reply(MessageDataImage.FromBase64(_songDb.SongList.RandomTake().GetImage()));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// mai什么推分
    /// </summary>
    [MarisaPluginSubCommand(nameof(MaiMaiDxPlayWhat))]
    [MarisaPluginCommand(true, "推分", "恰分", "上分", "加分")]
    private async Task<MarisaPluginTaskState> MaiMaiDxPlayWhatToUp(Message message)
    {
        var sender = message.Sender!.Id;

        // 拿b40
        try
        {
            var rating    = await GetDxRating(null, sender);
            var recommend = rating.GetRecommendCards(_songDb.SongList);

            if (recommend == null)
            {
                message.Reply("您无分可恰");
            }
            else
            {
                message.Reply(MessageDataImage.FromBase64(recommend.ToB64()));
            }
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            message.Reply("你谁啊？");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region list

    /// <summary>
    /// 给出歌曲列表
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "list", "ls")]
    private MarisaPluginTaskState MaiMaiDxListSong(Message message)
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

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 猜曲

    /// <summary>
    /// 舞萌猜歌排名
    /// </summary>
    [MarisaPluginSubCommand(nameof(MaiMaiDxGuess))]
    [MarisaPluginCommand(true, "排名")]
    private MarisaPluginTaskState MaiMaiDxGuessRank(Message message)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.MaiMaiDxGuesses
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
    /// 听歌猜曲
    /// </summary>
    [MarisaPluginSubCommand(nameof(MaiMaiDxGuess))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, true, "v2")]
    private MarisaPluginTaskState MaiMaiDxGuessV2(Message message, long qq)
    {
        StartSongSoundGuess(message, qq);
        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 舞萌猜歌
    /// </summary>
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MarisaPluginTaskState MaiMaiDxGuess(Message message, long qq)
    {
        if (message.Command.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
        {
            var reg = message.Command[2..];

            if (!reg.IsRegex())
            {
                message.Reply("错误的正则表达式：" + reg);
            }
            else
            {
                _songDb.StartSongCoverGuess(message, qq, 3, song =>
                    new Regex(reg, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)
                        .IsMatch(song.Info.Genre));
            }
        }
        else if (message.Command == "")
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

    #region 歌曲别名相关

    /// <summary>
    /// 别名处理
    /// </summary>
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private static MarisaPluginTaskState MaiMaiDxSongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 获取别名
    /// </summary>
    [MarisaPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "get")]
    private MarisaPluginTaskState MaiMaiDxSongAliasGet(Message message)
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
    [MarisaPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "set")]
    private MarisaPluginTaskState MaiMaiDxSongAliasSet(Message message)
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

    #region 分数线 / 容错率

    /// <summary>
    /// 分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState MaiMaiDxSongLine(Message message)
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

                message.Reply(ret);
                return MarisaPluginTaskState.CompletedTask;
            }
        }

        message.Reply("参数应为“定数”");
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}