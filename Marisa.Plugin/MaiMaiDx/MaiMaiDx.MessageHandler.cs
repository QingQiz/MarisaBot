using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class MaiMaiDx
{
    #region 搜歌

    [MarisaPluginNoDoc]
    [MarisaPluginCommand(true, "nocover")]
    private async Task<MarisaPluginTaskState> NoCover(Message message)
    {
        var noCover = SongDb.SongList.Where(s => s.NoCover);

        await SongDb.MultiPageSelectResult(noCover.ToList(), message);

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 绑定

    [MarisaPluginDisabled]
    [MarisaPluginDoc("绑定某个查分器")]
    [MarisaPluginCommand("bind", "绑定")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private static Task<MarisaPluginTaskState> Bind(Message message)
    {
        var fetchers = new[]
        {
            "DivingFish", "Wahlap"
        };

        message.Reply("请选择查分器（序号）：\n\n" + string.Join('\n', fetchers
            .Select((x, i) => (x, i))
            .Select(x => $"{x.i}. {x.x}"))
        );

        /*
         * 0 -> 检查输入的index，正确时询问access code
         * 1 -> 检查输入的access code
         */
        var stat = 0;

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), async next =>
        {
            switch (stat)
            {
                case 0:
                {
                    if (!int.TryParse(next.Command.Span, out var idx) || idx < 0 || idx >= fetchers.Length)
                    {
                        next.Reply("错误的序号，会话已关闭");
                        return MarisaPluginTaskState.CompletedTask;
                    }

                    if (idx == 0)
                    {
                        await using var dbContext = new BotDbContext();

                        var bind = dbContext.MaiMaiBinds.FirstOrDefault(x => x.UId == next.Sender.Id);

                        if (bind != null)
                        {
                            dbContext.MaiMaiBinds.Remove(bind);
                            await dbContext.SaveChangesAsync();
                        }

                        message.Reply("好了");
                        return MarisaPluginTaskState.CompletedTask;
                    }

                    // message.Reply("给出你舞萌在有效期内的二维码的扫描结果（以SGWC开头的字符串）");
                    // stat = 1;
                    //
                    // return MarisaPluginTaskState.ToBeContinued;

                    message.Reply("作者的服务器ip被Aime服务器ban了，暂时无法绑定。你可以使用的电脑获取AimeId，详情联系作者😢");
                    return MarisaPluginTaskState.CompletedTask;
                }
                case 1:
                {
                    var accessCode = next.Command.Trim();

                    try
                    {
                        var aimeId = await AllNetDataFetcher.GetUserId(accessCode);

                        await using var dbContext = new BotDbContext();

                        dbContext.MaiMaiBinds.Add(new MaiMaiDxBind(next.Sender.Id, aimeId));

                        await dbContext.SaveChangesAsync();

                        message.Reply("好了");
                    }
                    catch (InvalidDataException e)
                    {
                        message.Reply($"错误的二维码结果: {e.Message}。会话已关闭");
                    }

                    return MarisaPluginTaskState.CompletedTask;
                }
            }

            return MarisaPluginTaskState.CompletedTask;
        });

        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region unlock

    [MarisaPluginDisabled]
    [MarisaPluginDoc("逃离小黑屋")]
    [MarisaPluginCommand("unlock", "解锁")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private static async Task<MarisaPluginTaskState> UnLock(Message message)
    {
        await using var dbContext = new BotDbContext();

        var bind = dbContext.MaiMaiBinds.FirstOrDefault(x => x.UId == message.Sender.Id);

        if (bind == null)
        {
            message.Reply("你未绑定Wahlap，无法使用该功能");
            return MarisaPluginTaskState.CompletedTask;
        }

        var res = await AllNetDataFetcher.Logout(bind.AimeId);

        if (!res)
        {
            message.Reply("解锁失败。。。");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("妥了，玩吧。");
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 查分

    [MarisaPluginDisabled]
    [MarisaPluginDoc("从华丽服务前拉一次分，下一个该命令之前一直使用这次拉下来的分，避免重复请求")]
    [MarisaPluginCommand("fetch")]
    private async Task<MarisaPluginTaskState> Fetch(Message message)
    {
        await using var dbContext = new BotDbContext();

        var bind = dbContext.MaiMaiBinds.FirstOrDefault(x => x.UId == message.Sender.Id);

        if (bind == null)
        {
            message.Reply("你未绑定Wahlap，无法使用该功能");
            return MarisaPluginTaskState.CompletedTask;
        }

        await AllNetDataFetcher.Fetch(bind.AimeId);

        message.Reply("1");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     b50
    /// </summary>
    [MarisaPluginDoc("查询 b50，参数为：查分器的账号名 或 @某人 或 留空")]
    [MarisaPluginCommand("best", "b50", "查分")]
    private async Task<MarisaPluginTaskState> B50(Message message)
    {
        var fetcher = GetDataFetcher(message, true);

        var b50 = await fetcher.GetRating(message);

        var context = new WebContext();

        context.Put("b50", b50.ToJson());

        message.Reply(MessageChain.FromImageB64(await WebApi.MaiMaiBest(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 汇总 / summary

    [MarisaPluginDoc("获取成绩汇总，可以 @某人 查他的汇总")]
    [MarisaPluginCommand("summary", "sum")]
    private static async Task<MarisaPluginTaskState> Summary(Message message)
    {
        message.Reply("错误的命令格式");

        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("新谱的成绩汇总，无参数")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("new", "新谱")]
    private async Task<MarisaPluginTaskState> SummaryNew(Message message)
    {
        var fetcher = GetDataFetcher(message);

        // 旧谱的操作和新谱的一样，所以直接复制了，为这两个抽象一层有点不值
        var groupedSong = SongDb.SongList
            .Where(song => song.Info.IsNew)
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i >= 2)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var scores = await fetcher.GetScores(message);

        var im = await Task.Run(() => MaiMaiDraw.DrawGroupedSong(groupedSong, scores));
        // 一定不是空的
        message.Reply(MessageDataImage.FromBase64(im!.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取某定数的成绩汇总，参数为：定数1-定数2 或 定数")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("base", "b")]
    private async Task<MarisaPluginTaskState> SummaryBase(Message message)
    {
        var constants = message.Command.Split('-').Select(x =>
        {
            var res = double.TryParse(x.Trim().Span, out var c);
            return res ? c : -1;
        }).ToList();

        if (constants.Count is > 2 or < 1 || constants.Any(c => c < 1) || constants.Any(c => c > 15))
        {
            message.Reply("错误的命令格式");
        }
        else
        {
            if (constants.Count == 1)
            {
                constants.Add(constants[0]);
            }

            // 太大的话画图会失败，所以给判断一下
            if (constants[1] - constants[0] > 3)
            {
                message.Reply("过大的跨度");
                return MarisaPluginTaskState.CompletedTask;
            }

            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

            var groupedSong = SongDb.SongList
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(x => x.constant >= constants[0] && x.constant <= constants[1])
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.constant.ToString("F1"));

            var im = await Task.Run(() => MaiMaiDraw.DrawGroupedSong(groupedSong, scores));

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

    [MarisaPluginDoc("获取类别的成绩汇总，参数为：类别")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("genre", "type")]
    private async Task<MarisaPluginTaskState> SummaryGenre(Message message)
    {
        var genres = SongDb.SongList.Select(song => song.Info.Genre).Distinct().ToArray();

        var genre = genres.FirstOrDefault(p =>
            p.Equals(message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (genre == null)
        {
            message.Reply("可用的类别有：\n" + string.Join('\n', genres));
        }
        else
        {
            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

            var groupedSong = SongDb.SongList
                .Where(song => song.Info.Genre == genre)
                .Select(song => song.Constants
                    .Select((constant, i) => (constant, i, song)))
                .SelectMany(s => s)
                .Where(data => data.i >= 2)
                .OrderByDescending(x => x.constant)
                .GroupBy(x => x.song.Levels[x.i]);

            var im = await Task.Run(() => MaiMaiDraw.DrawGroupedSong(groupedSong, scores));

            // 不可能是 null
            message.Reply(MessageDataImage.FromBase64(im!.ToB64()));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取版本的成绩汇总，参数为：版本名")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("version", "ver")]
    private async Task<MarisaPluginTaskState> SummaryVersion(Message message)
    {
        var version = MaiMaiSong.Plates.FirstOrDefault(p =>
            p.Equals(message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (version == null)
        {
            var v = ConfigurationManager.Configuration.MaiMai.Version
                .Where(x => x.Value
                    .Contains(message.Command.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (v.Count == 0)
            {
                message.Reply("可用的版本号有：\n" + string.Join('\n', MaiMaiSong.Plates) + "\n（或者你也可以用一些别名）");
                return MarisaPluginTaskState.CompletedTask;
            }

            version = v.First().Key;
        }

        var versions = version.Split(',');

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = SongDb.SongList
            .Where(song => versions.Contains(song.Version, StringComparer.OrdinalIgnoreCase))
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i == 3)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var im = await Task.Run(() => MaiMaiDraw.DrawGroupedSong(groupedSong, scores));

        // 不可能是 null
        message.Reply(MessageDataImage.FromBase64(im!.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取某个难度的成绩汇总，参数为：难度")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("level", "lv")]
    private async Task<MarisaPluginTaskState> SummaryLevel(Message message)
    {
        var lv = message.Command.Trim();

        if (LvRegex().IsMatch(lv.ToString()))
        {
            var maxLv = lv.Span[^1] == '+' ? 14 : 15;
            var lvNr  = lv.Span[^1] == '+' ? lv[..^1] : lv;

            if (int.TryParse(lvNr.Span, out var i))
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

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = SongDb.SongList
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i].Equals(lv, StringComparison.Ordinal))
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await Task.Run(() => MaiMaiDraw.DrawGroupedSong(groupedSong, scores));

        // 不可能是 null
        message.Reply(MessageDataImage.FromBase64(im!.ToB64()));

        return MarisaPluginTaskState.CompletedTask;

        // 集中处理错误
        _error:
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 打什么歌

    [MarisaPluginDoc("如何推分到：参数")]
    [MarisaPluginCommand("howto", "how to")]
    private async Task<MarisaPluginTaskState> HowTo(Message message)
    {
        if (!int.TryParse(message.Command.Span, out var target))
        {
            message.Reply("参数不是数字");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var rating = await fetcher.GetRating(message with
        {
            Command = "".AsMemory()
        });

        var (old, @new, success) = GetRecommend(rating, target);

        if (!success)
        {
            message.Reply("no way");
            return MarisaPluginTaskState.CompletedTask;
        }

        var current = new
        {
            OldScores = rating.OldScores
                .Select(x => (SongDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4),
            NewScores = rating.NewScores
                .Select(x => (SongDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4)
        };

        var recommend = new
        {
            OldScores = old.OrderByDescending(x => x.Item4),
            NewScores = @new.OrderByDescending(x => x.Item4)
        };

        var context = new WebContext();

        context.Put("current", current);
        context.Put("recommend", recommend);

        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiRecommend(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     mai什么
    /// </summary>
    [MarisaPluginDoc("随机给出一个歌，参数任意")]
    [MarisaPluginCommand("打什么歌", "打什么", "什么")]
    private MarisaPluginTaskState PlayWhat(Message message)
    {
        message.Reply(MessageDataImage.FromBase64(SongDb.SongList.RandomTake().GetImage()));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     mai什么推分
    /// </summary>
    [MarisaPluginDoc("随机给出至多 4 首打了以后能推分的歌")]
    [MarisaPluginSubCommand(nameof(PlayWhat))]
    [MarisaPluginCommand(true, "推分", "恰分", "上分", "加分")]
    private async Task<MarisaPluginTaskState> PlayWhatToUp(Message message)
    {
        var fetcher   = GetDataFetcher(message);
        var rating    = await fetcher.GetRating(message);
        var recommend = rating.DrawRecommendCard(SongDb.SongList);

        if (recommend == null)
        {
            message.Reply("您无分可恰");
        }
        else
        {
            message.Reply(MessageDataImage.FromBase64(recommend.ToB64()));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 分数线 / 容错率

    /// <summary>
    ///     分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginDoc("给出定数对应的所有 rating 或 rating 对应的所有定数，参数为：歌曲定数 或 预期rating")]
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState RatingLine(Message message)
    {
        if (double.TryParse(message.Command.Span, out var constant))
        {
            switch (constant)
            {
                case <= 15.0 and >= 1:
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
                case > 15:
                {
                    var result = new List<(double Constant, double Achievement)>();
                    var ret    = "定数 -> 达成率 -> rating\n";

                    Enumerable.Range(1, 150)
                        .Where(rat =>
                            SongScore.Ra(100.5, rat / 10.0) >= constant && SongScore.Ra(50, rat / 10.0) <= constant)
                        .ToList()
                        .ForEach(rat =>
                        {
                            var a = 49.0;
                            while (a < 100.5)
                            {
                                a = SongScore.NextRa(a, rat / 10.0);
                                var ra = SongScore.Ra(a, rat / 10.0);

                                if (ra != (int)constant) continue;

                                result.Add((rat / 10.0, a));
                                break;
                            }
                        });

                    ret += string.Join('\n',
                        result.Select(x => $"{x.Constant:00.0} -> {x.Achievement:000.0000} -> {(int)constant}"));

                    message.Reply(ret);
                    return MarisaPluginTaskState.CompletedTask;
                }
            }
        }

        message.Reply("参数应为“定数”");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("计算某首歌曲的容错率，参数为：歌名")]
    [MarisaPluginCommand("tolerance", "tol", "容错率")]
    private async Task<MarisaPluginTaskState> FaultTolerance(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = SongDb.SearchSong(songName);

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("难度和预期达成率？");
        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var command = next.Command.Trim();

            var levelName   = MaiMaiSong.LevelNameAll.Concat(MaiMaiSong.LevelNameZh).ToList();
            var level       = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            var levelPrefix = level ?? "";
            if (level != null) goto RightLabel;

            level = levelName.FirstOrDefault(n =>
                command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
            if (level != null)
            {
                levelPrefix = command.Span[0].ToString();
                goto RightLabel;
            }

            next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、难度全名的首字母或难度颜色");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);

            RightLabel:
            var parseSuccess = double.TryParse(command[levelPrefix.Length..].Span, out var achievement);

            if (!parseSuccess)
            {
                next.Reply("错误的达成率格式，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (achievement is > 101 or < 0)
            {
                next.Reply("你查**呢");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var levelIdx = levelName.IndexOf(level) % MaiMaiSong.LevelNameAll.Count;
            var (x, y) = song.NoteScore(levelIdx);

            var tolerance = (int)((101 - achievement) / (0.2 * x));
            var dxScore   = song.Charts[levelIdx].Notes.Sum() * 3;

            var dxScores = new[]
                {
                    0.85, 0.9, 0.93, 0.95, 0.97
                }
                .Select(mul => ((int)Math.Ceiling(dxScore * mul), dxScore - (int)Math.Ceiling(dxScore * mul)))
                .ToArray();

            next.Reply(
                new MessageDataText($"[{MaiMaiSong.LevelNameAll[levelIdx]}] {song.Title} => {achievement:F4}\n"),
                new MessageDataText($"至多粉 {tolerance} 个 TAP，每个减 {0.2 * x:F4}%\n"),
                new MessageDataText($"绝赞 50 落相当于粉 {0.25 * y / (0.2 * x):F4} 个 TAP，每 50 落减 {0.25 * y:F4}%\n"),
                new MessageDataText($"\nDX分：{dxScore}\n"),
                new MessageDataText($"★ 最低 {dxScores[0].Item1}(-{dxScores[0].Item2})\n"),
                new MessageDataText($"★★ 最低 {dxScores[1].Item1}(-{dxScores[1].Item2})\n"),
                new MessageDataText($"★★★ 最低 {dxScores[2].Item1}(-{dxScores[2].Item2})\n"),
                new MessageDataText($"★★★★ 最低 {dxScores[3].Item1}(-{dxScores[3].Item2})\n"),
                new MessageDataText($"★★★★★ 最低 {dxScores[4].Item1}(-{dxScores[4].Item2})\n"),
                new MessageDataText("每小DX分减1，每粉DX分减2，否则DX分减3\n"),
                MessageDataImage.FromBase64(MaiMaiDraw.DrawFaultTable(x, y).ToB64())
            );
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });


        return MarisaPluginTaskState.CompletedTask;
    }

    [GeneratedRegex(@"^[0-9]+\+?$")]
    private static partial Regex LvRegex();

    #endregion
}