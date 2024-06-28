using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

[MarisaPluginDoc("音游 maimai DX 的相关功能")]
[MarisaPlugin(PluginPriority.MaiMaiDx)]
[MarisaPluginCommand("maimai", "mai", "舞萌")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class MaiMaiDx : MarisaPluginBaseWithHelpCommand
{

    #region 查分

    /// <summary>
    ///     b50
    /// </summary>
    [MarisaPluginDoc("查询 b50，参数为：查分器的账号名 或 @某人 或 留空")]
    [MarisaPluginCommand("best", "b50", "查分")]
    private async Task<MarisaPluginTaskState> MaiMaiDxB50(Message message)
    {
        var ret = await GetB50Card(message);

        message.Reply(ret);

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 搜歌

    /// <summary>
    ///     搜歌
    /// </summary>
    [MarisaPluginDoc("搜歌，参数为：歌曲名 或 歌曲别名 或 歌曲id")]
    [MarisaPluginCommand("song", "search", "搜索")]
    private MarisaPluginTaskState MaiMaiDxSearchSong(Message message)
    {
        return _songDb.SearchSong(message);
    }

    #endregion

    [MarisaPluginNoDoc]
    [MarisaPluginCommand(true, "nocover")]
    private MarisaPluginTaskState NoCover(Message message)
    {
        var coverPath = ResourceManager.ResourcePath + "/cover";

        var noCover = _songDb.SongList
            .Where(s => !File.Exists($"{coverPath}/{s.Id}.jpg") && !File.Exists($"{coverPath}/{s.Id}.png"));

        _songDb.MultiPageSelectResult(noCover.ToList(), message);

        return MarisaPluginTaskState.CompletedTask;
    }

    #region 绑定

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

        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, async next =>
        {
            switch (stat)
            {
                case 0:
                {
                    if (!int.TryParse(next.Command, out var idx) || idx < 0 || idx >= fetchers.Length)
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

    [MarisaPluginDoc("逃离小黑屋")]
    [MarisaPluginCommand("unlock", "解锁")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private static async Task<MarisaPluginTaskState> UnLock(Message message)
    {
        await using var dbContext = new BotDbContext();

        var bind = dbContext.MaiMaiBinds.FirstOrDefault(x => x.UId == message.Sender!.Id);

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
    #region 汇总 / summary

    [MarisaPluginDoc("获取成绩汇总，可以 @某人 查他的汇总")]
    [MarisaPluginCommand("summary", "sum")]
    private static async Task<MarisaPluginTaskState> MaiMaiSummary(Message message)
    {
        message.Reply("错误的命令格式");

        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("旧谱的成绩汇总，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("old", "旧谱")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryOld(Message message)
    {
        var fetcher = GetDataFetcher(message);
        // 旧谱的操作和新谱的一样，所以直接复制了，为这两个抽象一层有点不值
        var groupedSong = _songDb.SongList
            .Where(song => !song.Info.IsNew)
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

    [MarisaPluginDoc("新谱的成绩汇总，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("new", "新谱")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryNew(Message message)
    {
        var fetcher = GetDataFetcher(message);

        // 旧谱的操作和新谱的一样，所以直接复制了，为这两个抽象一层有点不值
        var groupedSong = _songDb.SongList
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
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("base", "b")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryBase(Message message)
    {
        var constants = message.Command.Split('-').Select(x =>
        {
            var res = double.TryParse(x.Trim(), out var c);
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

            var groupedSong = _songDb.SongList
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
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("genre", "type")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryGenre(Message message)
    {
        var genres = _songDb.SongList.Select(song => song.Info.Genre).Distinct().ToArray();

        var genre = genres.FirstOrDefault(p =>
            string.Equals(p, message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (genre == null)
        {
            message.Reply("可用的类别有：\n" + string.Join('\n', genres));
        }
        else
        {
            var fetcher = GetDataFetcher(message);
            var scores  = await fetcher.GetScores(message);

            var groupedSong = _songDb.SongList
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
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("version", "ver")]
    private async Task<MarisaPluginTaskState> MaiMaiSummaryVersion(Message message)
    {
        var version = MaiMaiSong.Plates.FirstOrDefault(p =>
            string.Equals(p, message.Command.Trim(), StringComparison.OrdinalIgnoreCase));

        if (version == null)
        {
            var v = ConfigurationManager.Configuration.MaiMai.Version
                .Where(x => x.Value
                    .Contains(message.Command.Trim(), StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!v.Any())
            {
                message.Reply("可用的版本号有：\n" + string.Join('\n', MaiMaiSong.Plates) + "\n（或者你也可以用一些别名）");
                return MarisaPluginTaskState.CompletedTask;
            }

            version = v.First().Key;
        }

        var versions = version.Split(',');

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = _songDb.SongList
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
    [MarisaPluginSubCommand(nameof(MaiMaiSummary))]
    [MarisaPluginCommand("level", "lv")]
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

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        var groupedSong = _songDb.SongList
            .Select(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .SelectMany(s => s)
            .Where(data => data.song.Levels[data.i] == lv)
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
    private async Task<MarisaPluginTaskState> MaiMaiDxHowTo(Message message)
    {
        if (!int.TryParse(message.Command, out var target))
        {
            message.Reply("参数不是数字");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var rating = await fetcher.GetRating(message with
        {
            Command = ""
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
                .Select(x => (_songDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
                .OrderByDescending(x => x.Item4),
            NewScores = rating.NewScores
                .Select(x => (_songDb.GetSongById(x.Id)!, x.LevelIdx, x.Achievement, x.Rating))
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
    private MarisaPluginTaskState MaiMaiDxPlayWhat(Message message)
    {
        message.Reply(MessageDataImage.FromBase64(_songDb.SongList.RandomTake().GetImage()));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     mai什么推分
    /// </summary>
    [MarisaPluginDoc("随机给出至多 4 首打了以后能推分的歌")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxPlayWhat))]
    [MarisaPluginCommand(true, "推分", "恰分", "上分", "加分")]
    private async Task<MarisaPluginTaskState> MaiMaiDxPlayWhatToUp(Message message)
    {
        var fetcher   = GetDataFetcher(message);
        var rating    = await fetcher.GetRating(message);
        var recommend = rating.DrawRecommendCard(_songDb.SongList);

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

    #region 随机

    /// <summary>
    ///     随机
    /// </summary>
    [MarisaPluginDoc("随机给出一个符合条件的歌曲")]
    [MarisaPluginCommand("random", "rand", "随机")]
    private async Task<MarisaPluginTaskState> MaiMaiDxRandomSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongBase(Message message)
    {
        _songDb.SelectSongByBaseRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand("charter", "谱师")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongCharter(Message message)
    {
        _songDb.SelectSongByCharter(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongLevel(Message message)
    {
        _songDb.SelectSongByLevel(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand("bpm")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongBpm(Message message)
    {
        _songDb.SelectSongByBpmRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand("artist", "a")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongArtist(Message message)
    {
        _songDb.SelectSongByArtist(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出一个新歌，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand(true, "new", "新谱")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongNew(Message message)
    {
        SelectSongWhenNew().RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出一个老歌，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxRandomSong))]
    [MarisaPluginCommand(true, "old", "旧谱")]
    private Task<MarisaPluginTaskState> MaiMaiDxRandomSongOld(Message message)
    {
        SelectSongWhenOld().RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 筛选

    /// <summary>
    ///     给出歌曲列表
    /// </summary>
    [MarisaPluginDoc("给出符合条件的歌曲，结果过多时回复 p1、p2 等获取额外的信息")]
    [MarisaPluginCommand("list", "ls")]
    private async Task<MarisaPluginTaskState> MaiMaiDxListSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongBase(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByBaseRange(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand("charter", "谱师")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongCharter(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByCharter(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongLevel(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByLevel(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand("bpm")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongBpm(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByBpmRange(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand("artist", "a")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongArtist(Message message)
    {
        _songDb.MultiPageSelectResult(_songDb.SelectSongByArtist(message.Command), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出新谱面，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand(true, "new", "新谱")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongNew(Message message)
    {
        _songDb.MultiPageSelectResult(SelectSongWhenNew(), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出旧谱面，无参数")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxListSong))]
    [MarisaPluginCommand(true, "old", "旧谱")]
    private Task<MarisaPluginTaskState> MaiMaiDxListSongOld(Message message)
    {
        _songDb.MultiPageSelectResult(SelectSongWhenOld(), message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 猜曲

    /// <summary>
    ///     舞萌猜歌排名
    /// </summary>
    [MarisaPluginDoc("舞萌猜歌的排名，给出的结果中s,c,w分别是启动猜歌的次数，猜对的次数和猜错的次数")]
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
    ///     舞萌猜歌
    /// </summary>
    [MarisaPluginDoc("舞萌猜歌，看封面猜曲")]
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
    ///     别名处理
    /// </summary>
    [MarisaPluginDoc("别名设置和查询")]
    [MarisaPluginCommand("alias")]
    private static MarisaPluginTaskState MaiMaiDxSongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     获取别名
    /// </summary>
    [MarisaPluginDoc("获取别名，参数为：歌名/别名")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MarisaPluginCommand("get")]
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
    ///     设置别名
    /// </summary>
    [MarisaPluginDoc("设置别名，参数为：歌曲原名 或 歌曲id := 歌曲别名")]
    [MarisaPluginSubCommand(nameof(MaiMaiDxSongAlias))]
    [MarisaPluginCommand("set")]
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
    ///     分数线，达到某个达成率rating会上升的线
    /// </summary>
    [MarisaPluginDoc("给出定数对应的所有 rating 或 rating 对应的所有定数，参数为：歌曲定数 或 预期rating")]
    [MarisaPluginCommand("line", "分数线")]
    private static MarisaPluginTaskState MaiMaiDxRatingLine(Message message)
    {
        if (double.TryParse(message.Command, out var constant))
        {
            if (constant is <= 15.0 and >= 1)
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

            if (constant > 15)
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

                            if (ra == (int)constant)
                            {
                                result.Add((rat / 10.0, a));
                                break;
                            }
                        }
                    });

                ret += string.Join('\n',
                    result.Select(x => $"{x.Constant:00.0} -> {x.Achievement:000.0000} -> {(int)constant}"));

                message.Reply(ret);
                return MarisaPluginTaskState.CompletedTask;
            }
        }

        message.Reply("参数应为“定数”");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("计算某首歌曲的容错率，参数为：歌名")]
    [MarisaPluginCommand("tolerance", "容错率")]
    private MarisaPluginTaskState MaiMaiFaultTolerance(Message message)
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

            var levelName   = MaiMaiSong.LevelName.Concat(MaiMaiSong.LevelNameZh).ToList();
            var level       = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            var levelPrefix = level ?? "";
            if (level != null) goto RightLabel;

            level = levelName.FirstOrDefault(n =>
                command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
            if (level != null)
            {
                levelPrefix = command[0].ToString();
                goto RightLabel;
            }

            next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、难度全名的首字母或难度颜色");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);

            RightLabel:
            var parseSuccess = double.TryParse(command.TrimStart(levelPrefix), out var achievement);

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

            var song = searchResult.First();

            var levelIdx = levelName.IndexOf(level) % MaiMaiSong.LevelName.Count;
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
                new MessageDataText($"[{MaiMaiSong.LevelName[levelIdx]}] {song.Title} => {achievement:F4}\n"),
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

    #endregion
}