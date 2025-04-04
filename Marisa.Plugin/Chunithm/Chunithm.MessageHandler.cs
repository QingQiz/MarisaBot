using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Chunithm;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Chunithm
{
    #region 绑定

    [MarisaPluginDoc("绑定某个查分器")]
    [MarisaPluginCommand("bind", "绑定")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private Task<MarisaPluginTaskState> Bind(Message message)
    {
        var fetchers = new[]
        {
            "DivingFish",
            "Louis",
            "RinNET",
            "Aqua",
            "其它基于AllNet/Aqua/Chusan的服务"
        };

        message.Reply("请选择查分器（序号）：\n\n" + string.Join('\n', fetchers
            .Select((x, i) => (x, i))
            .Select(x => $"{x.i}. {x.x}"))
        );

        /*
         * 0 -> 检查输入的index
         * 1 -> 检查输入的服务器地址
         * 2 -> 询问access code
         * 3 -> 检查输入的access code
         */
        var stat   = 0;
        var server = "";

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            switch (stat)
            {
                case 0:
                {
                    if (!int.TryParse(next.Command.Span, out var idx) || idx < 0 || idx >= fetchers.Length)
                    {
                        next.Reply("错误的序号，会话已关闭");
                        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                    }

                    if (idx is 0 or 1)
                    {
                        using var dbContext = new BotDbContext();

                        var bind = dbContext.ChunithmBinds.FirstOrDefault(x => x.UId == next.Sender.Id);

                        if (bind == null)
                        {
                            dbContext.ChunithmBinds.Add(new ChunithmBind(next.Sender.Id, fetchers[idx]));
                        }
                        else
                        {
                            bind.ServerName = fetchers[idx];
                            bind.AccessCode = "";
                            dbContext.ChunithmBinds.Update(bind);
                        }
                        dbContext.SaveChanges();

                        message.Reply("好了");
                        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                    }

                    if (idx == fetchers.Length - 1)
                    {
                        message.Reply("给出服务器的地址，即你填写在segatools.ini的[dns]中default的值");
                        stat = 1;
                    }
                    else
                    {
                        server = fetchers[idx];
                        message.Reply("给出你Aime卡的Access Code，即Aime卡背面的值或你填写在aime.txt中的值");
                        stat = 2;
                    }

                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }
                case 1:
                {
                    var host = next.Command.Trim();
                    try
                    {
                        var hostStr = host.ToString();
                        if (Dns.GetHostAddresses(hostStr).Length != 0)
                        {
                            server = hostStr;
                            message.Reply("给出你Aime卡的Access Code，即Aime卡背面的值或你填写在aime.txt中的值");
                            stat = 2;
                            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                        }
                    }
                    catch (ArgumentException) {}

                    next.Reply($"无效的服务器地址{host}，会话已关闭");
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }
                case 2:
                {
                    var accessCode = next.Command.Replace("-", "").Trim().ToString();

                    if (accessCode.Length != 20)
                    {
                        next.Reply($"无效的Access Code: {accessCode}，长度应为20");
                        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                    }

                    using var dbContext = new BotDbContext();

                    var bind = dbContext.ChunithmBinds.FirstOrDefault(x => x.UId == next.Sender.Id);

                    if (bind == null)
                    {
                        bind = new ChunithmBind(next.Sender.Id, server, accessCode);
                    }
                    else
                    {
                        bind.ServerName = server;
                        bind.AccessCode = accessCode;
                    }

                    var fetcher = GetDataFetcher(server, bind);

                    if (!(fetcher as AllNetBasedNetDataFetcher)!.Test(accessCode))
                    {
                        next.Reply($"该Access Code尚未在{server}中绑定");
                        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                    }

                    dbContext.ChunithmBinds.InsertOrUpdate(bind);
                    dbContext.SaveChanges();

                    message.Reply("好了");

                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }
            }

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 搜歌

    /// <summary>
    ///     谱面预览
    /// </summary>
    [MarisaPluginDoc("谱面预览，参数为：歌曲名 或 歌曲别名 或 歌曲id")]
    [MarisaPluginCommand("preview", "view", "谱面", "预览")]
    private async Task<MarisaPluginTaskState> PreviewSong(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = SongDb.SearchSong(songName);

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply($"哪个？\n\n{string.Join('\n', song.Levels
            .Select((l, i) =>
                $"{i}. [{song.DiffNames[i]}] {l}{(string.IsNullOrEmpty(song.ChartName[i]) ? " 无数据" : "")}"
            ).ToList())
        }");

        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var command = next.Command.Trim();

            if (!int.TryParse(command.Span, out var levelIdx) || levelIdx < 0 || levelIdx >= song.Levels.Count)
            {
                next.Reply("错误的选择，请选择前面的编号。会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (string.IsNullOrEmpty(song.ChartName[levelIdx]))
            {
                next.Reply("暂无该难度的数据");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var chartName = song.ChartName[levelIdx];
            var cacheName = Path.Join(ResourceManager.TempPath, $"Preview-{chartName}.b64");
            var img = new CacheableText(cacheName, () =>
            {
                var ctx = new WebContext();
                ctx.Put("chart", File.ReadAllText(
                    Path.Join(ResourceManager.ResourcePath, "chart", chartName + ".c2s")
                ));
                return WebApi.ChunithmPreview(ctx.Id).Result;
            });

            message.Reply(
                new MessageDataText($"[{song.DiffNames[levelIdx]}] {song.Title}"),
                MessageDataImage.FromBase64(img.Value)
            );

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

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

        if (constants.Count is > 2 or < 1 || constants.Any(c => c < 1) || constants.Any(c => c > 16))
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (constants.Count == 1)
        {
            constants.Add(constants[0]);
        }

        // 太大的话画图会失败，所以给判断一下
        if (constants[1] - constants[0] > 2)
        {
            message.Reply("过大的跨度，最多是2");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = await GetDataFetcher(message);

        var scores = await fetcher.GetScores(message);

        var groupedSong = fetcher.GetSongList()
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(x => x.constant >= constants[0] && x.constant <= constants[1])
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await ChunithmDraw.DrawGroupedSong(groupedSong, scores);

        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取类别的成绩汇总，参数为：类别")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("genre", "type")]
    private async Task<MarisaPluginTaskState> SummaryGenre(Message message)
    {
        var fetcher = await GetDataFetcher(message);

        var genres = fetcher.GetSongList().Select(song => song.Genre).Distinct().ToArray();

        var genre = genres.FirstOrDefault(p =>
            message.Command.Trim().Equals(p, StringComparison.OrdinalIgnoreCase));

        if (genre == null)
        {
            message.Reply("可用的类别有：\n" + string.Join('\n', genres));
            return MarisaPluginTaskState.CompletedTask;
        }

        var scores = await fetcher.GetScores(message);

        var groupedSong = fetcher.GetSongList()
            .Where(song => song.Genre == genre)
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i >= 3 && data.song.Constants[data.i] > 0)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var im = await ChunithmDraw.DrawGroupedSong(groupedSong, scores);

        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取版本的成绩汇总，参数为：版本名")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("version", "ver")]
    private async Task<MarisaPluginTaskState> SummaryVersion(Message message)
    {
        var fetcher = await GetDataFetcher(message);

        var versions = fetcher.GetSongList().Select(song => song.Version).Distinct().ToArray();

        var version = versions.FirstOrDefault(p =>
            message.Command.Trim().Equals(p, StringComparison.OrdinalIgnoreCase));

        if (version == null)
        {
            message.Reply("可用的版本有：\n" + string.Join('\n', versions));
            return MarisaPluginTaskState.CompletedTask;
        }

        var scores = await fetcher.GetScores(message);

        var groupedSong = fetcher.GetSongList()
            .Where(song => song.Version == version)
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.i >= 3 && data.song.Constants[data.i] > 0)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.song.Levels[x.i]);

        var im = await ChunithmDraw.DrawGroupedSong(groupedSong, scores);

        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取某个难度的成绩汇总，参数为：难度")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("level", "lv")]
    private async Task<MarisaPluginTaskState> SummaryLevel(Message message)
    {
        var lv = message.Command.Trim().ToString();

        if (LevelRegex().IsMatch(lv))
        {
            const int maxLv = 15;
            var       lvNr  = lv.EndsWith('+') ? lv[..^1] : lv;

            if (int.TryParse(lvNr, out var i))
            {
                if (i is < 1 or > maxLv)
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

        var fetcher = await GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = fetcher.GetSongList()
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i] == lv)
            .OrderByDescending(x => x.constant)
            .GroupBy(x => x.constant.ToString("F1"));

        var im = await ChunithmDraw.DrawGroupedSong(groupedSong, scores);

        message.Reply(MessageDataImage.FromBase64(im));

        return MarisaPluginTaskState.CompletedTask;

        // 集中处理错误
        _error:
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("获取OverPower的统计")]
    [MarisaPluginSubCommand(nameof(Summary))]
    [MarisaPluginCommand("overpower", "op")]
    private async Task<MarisaPluginTaskState> SummaryOverPower(Message message)
    {
        var fetcher = await GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var songs = fetcher.GetSongList()
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s);

        var ctx = new WebContext();
        ctx.Put("OverPowerScores", scores);
        ctx.Put("OverPowerSongs", songs);

        var img = await WebApi.ChunithmOverPowerAll(ctx.Id);
        message.Reply(MessageDataImage.FromBase64(img));

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 查分

    /// <summary>
    ///     b30
    /// </summary>
    [MarisaPluginDoc("查询 b30，参数为：查分器的账号名 或 @某人 或 留空")]
    [MarisaPluginCommand("b30", "查分")]
    private async Task<MarisaPluginTaskState> B30(Message message)
    {
        var ret = await GetRatingImg(message);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("b30的汇总情况，具体的试一试命令就知道了（懒）")]
    [MarisaPluginSubCommand(nameof(B30))]
    [MarisaPluginCommand("sum")]
    private async Task<MarisaPluginTaskState> B30Sum(Message message)
    {
        var fetcher = await GetDataFetcher(message);
        var rating  = await fetcher.GetRating(message);

        var bSum = rating.Records.Best.Sum(x => x.Rating) * 100;
        var rSum = rating.Records.Recent.Sum(x => x.Rating) * 100;

        message.Reply($"{rating.Username} ({rating.Rating})\nBest: {rating.B30}\nRecent: {rating.R10}\n\n" +
                      $"推分剩余: 0.{40 - (bSum + rSum) % 40:00}\nBest 推分剩余: 0.{30 - bSum % 30:00}\nRecent 推分剩余: 0.{10 - rSum % 10:00}");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     b50
    /// </summary>
    [MarisaPluginDoc("查询 b50，参数为：查分器的账号名 或 @某人 或 留空")]
    [MarisaPluginCommand("b50")]
    private async Task<MarisaPluginTaskState> B50(Message message)
    {
        var ret = await GetRatingImg(message, true);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("b50的汇总情况，具体的试一试命令就知道了（懒）")]
    [MarisaPluginSubCommand(nameof(B50))]
    [MarisaPluginCommand("sum")]
    private async Task<MarisaPluginTaskState> B50Sum(Message message)
    {
        var rating = await GetRating(message, true);

        var bSum = rating.Records.Best.Sum(x => x.Rating) * 100;
        var rSum = rating.Records.Recent.Sum(x => x.Rating) * 100;

        message.Reply($"{rating.Username} ({rating.Rating})\nOld : {rating.B30}\nNew: {rating.R20}\n\n" +
                      $"推分剩余: 0.{50 - (bSum + rSum) % 50:00}\nOld 推分剩余: 0.{30 - bSum % 30:00}\nNew 推分剩余: 0.{20 - rSum % 20:00}");

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 分数线 / 容错率

    /// <summary>
    ///     分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginDoc("给出定数对应的一些 rating，参数为：歌曲定数")]
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState RatingLine(Message message)
    {
        if (decimal.TryParse(message.Command.Span, out var constant))
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
    [MarisaPluginCommand("tolerance", "tol", "容错率")]
    protected async Task<MarisaPluginTaskState> FaultTolerance(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = SongDb.SearchSong(songName);

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply($"序号和预期达成率？\n\n{string.Join('\n', song.Levels
            .Select((l, i) =>
                $"{i}. [{song.DiffNames[i]}] {l}{(string.IsNullOrEmpty(song.ChartName[i]) ? " 无数据" : "")}"
            ).ToList())
        }");

        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var command = next.Command.Trim();

            // 第一位是idx，后面是预期达成率
            if (!int.TryParse(command[..1].Span, out var levelIdx) || levelIdx < 0 || levelIdx >= song.Levels.Count)
            {
                next.Reply("错误的选择，请选择前面的编号。会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }
            if (song.MaxCombo[levelIdx] == 0)
            {
                next.Reply("暂无该难度的数据");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var parseSuccess = int.TryParse(command[1..].Trim().Span, out var achievement);
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
            var noteScore = 101_0000.0m / maxCombo;

            var greenScore = 50.0m / 101 * noteScore;
            var v绿减分       = noteScore - greenScore;
            var v小p减分      = 1.0m / 101 * noteScore;

            var greenCount     = (int)(tolerance / v绿减分);
            var grayCount      = (int)(tolerance / noteScore);
            var greenRemaining = tolerance - greenCount * v绿减分;
            var grayRemaining  = tolerance - grayCount * noteScore;

            next.Reply(
                new MessageDataText($"[{song.Levels[levelIdx]}] {song.Title} => {achievement}\n"),
                new MessageDataText($"至多绿 {greenCount} 个 + {(int)(greenRemaining / v小p减分)} 小\n"),
                new MessageDataText($"至多灰 {grayCount} 个 + {(int)(grayRemaining / v小p减分)} 小\n"),
                new MessageDataText($"每个绿减 {v绿减分:F2}，每个灰减 {noteScore:F2}，每小减 {v小p减分:F2}")
            );

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    [GeneratedRegex(@"^[0-9]+\+?$")]
    private static partial Regex LevelRegex();

    #endregion
}