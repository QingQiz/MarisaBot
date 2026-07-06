using System.Security.Cryptography;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private string[]? _versions;

    private string[] Versions => _versions ??= BuildVersionList(SongDb.SongList);

    private void ResetCaches()
    {
        _versions = null;
    }

    private static string[] BuildVersionList(IReadOnlyList<MaiMaiSong> songs)
    {
        return VersionOrderHelper.BuildVersionList(songs, song => song.Version, song => song.Id);
    }

    #region 等级/定数解析

    /// <summary>严格解析等级（纯数字可带尾加号，禁符号/空白/前导零；加号等级最高 14+），
    /// 输出规范串。宽松的 int.TryParse 会放行 "+13"/"013"/"13 +" 这类写法。</summary>
    private static bool TryParseLevel(string value, out string level)
    {
        level = "";
        var plus = value.EndsWith('+');
        var core = plus ? value[..^1] : value;

        if (core.Length is 0 or > 2 || !core.All(char.IsAsciiDigit) || core[0] == '0') return false;

        var lv = int.Parse(core);
        if (lv < 1 || lv > (plus ? 14 : 15)) return false;

        level = plus ? $"{lv}+" : $"{lv}";
        return true;
    }

    /// <summary>严格解析定数（X 或 X.X，一位小数，禁符号/千分位/NaN）。宽松的 double.TryParse
    /// 会放行 "NaN"（穿过范围比较）、"14.75"（被格式化静默取整）、zh-CN 下的 "1,4"（千分位）。</summary>
    private static bool TryParseConstant(string value, out double constant)
    {
        constant = 0;
        var dot = value.IndexOf('.');
        var ip  = dot < 0 ? value : value[..dot];
        var fp  = dot < 0 ? "0" : value[(dot + 1)..];

        if (ip.Length is 0 or > 2 || !ip.All(char.IsAsciiDigit) || ip[0] == '0') return false;
        if (fp.Length != 1 || !char.IsAsciiDigit(fp[0])) return false;

        constant = int.Parse(ip) + (fp[0] - '0') / 10.0;
        return constant is >= 1 and <= 15;
    }

    /// <summary>难度曲线数据的版本哈希（进程内取一次）：读前端分发的 difficulty_curves.json，
    /// 数据随前端更新后带哈希的缓存文件名自动翻新；取不到时按天退化。</summary>
    private static readonly Lazy<string> CurveDataHash = new(() =>
    {
        try
        {
            using var http = new HttpClient();
            var bytes = http.GetByteArrayAsync(
                ConfigurationManager.Configuration.Web.PrivateBaseUrl + "/assets/maimai/difficulty_curves.json").Result;
            return Convert.ToHexString(MD5.HashData(bytes))[..12];
        }
        catch
        {
            return DateTime.Today.ToString("yyyyMMdd");
        }
    });

    #endregion

    #region recommend

    private (List<(MaiMaiSong, int, double, int)> listOld, List<(MaiMaiSong, int, double, int)> listNew, bool)
        GetRecommend(DxRating rating, int targetRating)
    {
        var listOld = rating.OldScores
            .Select(score => (Song: SongDb.GetSongById(score.Id), score.LevelIdx, score.Achievement, score.Rating))
            .Where(x => x.Song != null)
            .Select(x => (x.Song!, x.LevelIdx, x.Achievement, x.Rating))
            .ToList();
        var listNew = rating.NewScores
            .Select(score => (Song: SongDb.GetSongById(score.Id), score.LevelIdx, score.Achievement, score.Rating))
            .Where(x => x.Song != null)
            .Select(x => (x.Song!, x.LevelIdx, x.Achievement, x.Rating))
            .ToList();

        var raOld = rating.OldScores.Sum(s => s.Ra());
        var raNew = rating.NewScores.Sum(s => s.Ra());

        var idSet = new HashSet<(long, int)>();
        foreach (var id in rating.OldScores.Select(s => (s.Id, s.LevelIdx)))
            idSet.Add(id);

        foreach (var id in rating.NewScores.Select(s => (s.Id, s.LevelIdx)))
            idSet.Add(id);

        var newSongList = SongDb.SongList.Where(s => s.Info.IsNew).ToList();
        var oldSongList = SongDb.SongList.Where(s => !s.Info.IsNew).ToList();

        double maxConst = 0;

        try
        {
            maxConst = Math.Max(rating.NewScores.Max(x => x.Constant), rating.OldScores.Max(x => x.Constant));
        }
        catch (InvalidOperationException)
        {
        }

        var fails = 0b1111;
        var rand  = new Random();
        while (true)
        {
            if (raOld + raNew >= targetRating) break;
            if (fails == 0) break;

            int r;
            do
            {
                r = rand.Next(4);
            } while ((1 << r & fails) == 0);

            switch (r)
            {
                case 0:
                {
                    var success = Update1(listOld, ref raOld, oldSongList, 35);

                    if (!success)
                        fails &= 0b1110;
                    else
                        fails |= 0b0100;
                    break;
                }
                case 1:
                {
                    var success = Update1(listNew, ref raNew, newSongList, 15);

                    if (!success)
                        fails &= 0b1101;
                    else
                        fails |= 0b1000;
                    break;
                }
                case 2:
                {
                    var success = Update2(listOld, ref raOld);

                    if (!success) fails &= 0b1011;
                    break;
                }
                case 3:
                {
                    var success = Update2(listNew, ref raNew);

                    if (!success) fails &= 0b0111;
                    break;
                }
            }
        }

        return (listOld, listNew, fails != 0);

        List<MaiMaiSong> GetAvailableSongs(IEnumerable<MaiMaiSong> songs, int minRa)
        {
            return songs
                .Where(s => s.Constants
                    .Select((c, i) => (c, i))
                    // 你妈逼的浮点误差，14.7 -> 14.699999999999999
                    .Any(c =>
                        !idSet.Contains((s.Id, c.i))
                     && c.c <= maxConst + 0.15
                     && SongScore.Ra(100.5, c.c) > minRa
                    )
                )
                .ToList();
        }

        bool Update1(IList<(MaiMaiSong Song, int Idx, double Achievement, int Rating)> list, ref int raSum,
            IEnumerable<MaiMaiSong> songList, int cap)
        {
            if (list.Count == 0) return false;

            var (oldSong, oldIdx, oldAchievement, oldRa) = list.MinBy(x => x.Rating);

            idSet.Remove((oldSong.Id, oldIdx));

            var songs = GetAvailableSongs(songList, oldRa);

            if (songs.Count == 0)
            {
                // revert change
                idSet.Add((oldSong.Id, oldIdx));

                return Update2(list, ref raSum);
            }

            var newSong = songs.RandomTake();

            var achievements = newSong.Constants.Select(c => SongScore.NextAchievement(c, oldRa)).ToList();
            var newIdx = achievements
                .Select((x, i) => (x, i)).ToList()
                .FindIndex(x => !idSet.Contains((newSong.Id, x.i)) && x.x > 0);
            var newAchievement = achievements[newIdx];
            var newRa          = SongScore.Ra(newAchievement, newSong.Constants[newIdx]);

            raSum -= oldRa;
            raSum += newRa;

            maxConst = Math.Max(maxConst, newSong.Constants[newIdx]);

            idSet.Add((newSong.Id, newIdx));
            if (list.Count >= cap) list.Remove((oldSong, oldIdx, oldAchievement, oldRa));
            list.Add((newSong, newIdx, newAchievement, newRa));
            return true;
        }

        bool Update2(IList<(MaiMaiSong Song, int Idx, double Achievement, int Rating)> list, ref int raSum)
        {
            var items = list.Select((x, i) => (x, i)).Where(x => x.x.Achievement < 100.5).ToList();

            if (!items.Any()) return false;

            var (item, idx) = items.RandomTake();

            var oldRa          = SongScore.Ra(item.Achievement, item.Song.Constants[item.Idx]);
            var newAchievement = SongScore.NextAchievement(item.Song.Constants[item.Idx], oldRa);
            var newRa          = SongScore.Ra(newAchievement, item.Song.Constants[item.Idx]);

            raSum -= oldRa;
            raSum += newRa;

            list[idx] = (item.Song, item.Idx, newAchievement, newRa);

            return true;
        }
    }

    #endregion

    #region data fetcher

    private DataFetcher GetDataFetcher(Message message, bool allowUsername = false)
    {
        // Command不为空的话，就是用用户名查。只有DivingFish能使用用户名查
        if (allowUsername && !message.Command.IsWhiteSpace())
        {
            return GetDataFetcher(DataFetcherType.DivingFish);
        }

        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<Marisa.Database.Entity.Plugin.MaiMaiDx.MaiMaiDxBind>().FirstOrDefault(x => x.UId == qq);

        if (bind == null)
        {
            return GetDataFetcher(DataFetcherType.DivingFish);
        }

        return bind.ServerName switch
        {
            "lxns" => GetDataFetcher(DataFetcherType.Lxns),
            "DivingFish" => GetDataFetcher(DataFetcherType.DivingFish),
            "Wahlap" => GetDataFetcher(DataFetcherType.Wahlap),
            _ => GetDataFetcher(DataFetcherType.Wahlap)
        };
    }

    private readonly Dictionary<DataFetcherType, DataFetcher> _dataFetchers = new();

    private DataFetcher GetDataFetcher(DataFetcherType type)
    {
        if (_dataFetchers.TryGetValue(type, out var fetcher)) return fetcher;

        return _dataFetchers[type] = type switch
        {
            DataFetcherType.DivingFish => new DivingFishDataFetcher(SongDb),
            DataFetcherType.Wahlap     => new AllNetDataFetcher(SongDb),
            DataFetcherType.Lxns       => new LxnsDataFetcher(SongDb),
            _                          => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private enum DataFetcherType
    {
        DivingFish,
        Wahlap,
        Lxns
    }

    #endregion

    #region label helpers

    // diving-fish fc/fs 字段 → 可读标记。fs 的 fsd/fsdp 是 FDX/FDX+ 的老命名。
    private static string FcLabel(string? fc) => fc switch
    {
        "app" => "AP+",
        "ap" => "AP",
        "fcp" => "FC+",
        "fc" => "FC",
        _ => ""
    };

    private static string FsLabel(string? fs) => fs switch
    {
        "fsdp" => "FDX+",
        "fsd" => "FDX",
        "fsp" => "FS+",
        "fs" => "FS",
        "sync" => "SYNC",
        _ => ""
    };

    #endregion
}