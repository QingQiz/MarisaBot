using System.Text.RegularExpressions;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;
using Microsoft.CSharp.RuntimeBinder;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.Chunithm;

public partial class ChunithmSong : Song
{
    public enum DataSource
    {
        DivingFish,
        Louis,
        Default
    }

    public static readonly Dictionary<string, Color> LevelColor = new()
    {
        { "BASIC", MaiMaiSong.LevelColor[0] },
        { "ADVANCED", MaiMaiSong.LevelColor[1] },
        { "EXPERT", MaiMaiSong.LevelColor[2] },
        { "MASTER", MaiMaiSong.LevelColor[3] },
        { "ULTIMA", Color.Black },
        { "WORLD'S END", MaiMaiSong.LevelColor.Last() }
    };

    public static readonly Dictionary<string, string> LevelAlias = new()
    {
        { "绿", "BASIC" },
        { "黄", "ADVANCED" },
        { "红", "EXPERT" },
        { "紫", "MASTER" },
        { "黑", "ULTIMA" },
        { "we", "WORLD'S END" }
    };

    public static readonly List<string> LevelLabel = ["BASIC", "ADVANCED", "EXPERT", "MASTER", "ULTIMA", "WORLD'S END"];

    public readonly List<string> ChartName = [];
    public readonly string Genre;
    /// <summary>
    ///     BASIC, ADVANCED, EXPERT, MASTER, ULTIMA, ...
    /// </summary>
    public readonly List<string> LevelName = [];
    public readonly List<long> MaxCombo = [];
    public readonly List<double> ConstantOld = [];

    private readonly List<string> _bpms = [];
    private List<double>? _bpmList;

    public List<double> BpmList => _bpmList ??= (
        from bpm in _bpms
        select DoubleRegex().Matches(bpm)
        into matches
        from match in matches
        select double.Parse(match.Value)
        into bpm
        select bpm
    ).ToList();

    public ChunithmSong(dynamic o, DataSource source = DataSource.Default)
    {
        switch (source)
        {
            case DataSource.DivingFish:
            {
                Id      = o.id;
                Title   = o.title;
                Artist  = o.basic_info.artist;
                Genre   = o.basic_info.genre;
                Version = o.basic_info.from;

                for (var i = 0; i < o.level.Count; i++)
                {
                    Constants.Add(o.ds[i]);
                    ConstantOld.Add(0);
                    Charters.Add(o.charts[i].charter);
                    Levels.Add(o.level[i]);
                    LevelName.Add(o.level[i]);
                    MaxCombo.Add(o.charts[i].combo);
                    ChartName.Add("");
                    _bpms.Add(o.basic_info.bpm.ToString());
                }
                break;
            }
            case DataSource.Louis:
            {
                Id      = o.musicID;
                Title   = o.title;
                Artist  = o.artist;
                Genre   = o.genre;
                Version = o.from;

                var charts = new[] { o.charts.basic, o.charts.advanced, o.charts.expert, o.charts.master, o.charts.ultima, o.charts.worldsend };
                for (var i = 0; i < charts.Length; i++)
                {
                    var chart = charts[i];
                    if (!chart.enabled) continue;

                    Constants.Add(chart.constant);
                    ConstantOld.Add(0);
                    Charters.Add(chart.charter);
                    Levels.Add(chart.level);
                    LevelName.Add(LevelLabel[i]);
                    MaxCombo.Add(0);
                    ChartName.Add("");
                }
                break;
            }
            case DataSource.Default:
            {
                Id      = o.Id;
                Title   = o.Title;
                Artist  = o.Artist;
                Genre   = o.Genre;
                Version = o.Version;

                foreach (var i in o.Beatmaps)
                {
                    Constants.Add(i.Constant);
                    ConstantOld.Add(0);
                    Charters.Add(i.Charter);
                    Levels.Add(i.LevelStr);
                    LevelName.Add(i.LevelName);
                    ChartName.Add(i.ChartName);
                    _bpms.Add(i.Bpm);

                    try
                    {
                        MaxCombo.Add(i.MaxCombo);
                    }
                    catch (RuntimeBinderException)
                    {
                        MaxCombo.Add(0);
                    }
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }

    public static decimal Ra(int achievement, decimal constant)
    {
        var res = achievement switch
        {
            >= 100_9000 => constant + 2.15m,
            >= 100_7500 => constant + 2.0m + (achievement - 100_7500m) / (100_9000m - 100_7500m) * (2.15m - 2.0m),
            >= 100_5000 => constant + 1.5m + (achievement - 100_5000m) / (100_7500m - 100_5000m) * (2.0m - 1.5m),
            >= 100_0000 => constant + 1.0m + (achievement - 100_0000m) / (100_5000m - 100_0000m) * (1.5m - 1.0m),
            >= 97_5000  => constant + 0.0m + (achievement - 97_5000m) / (100_0000m - 97_5000m) * (1.0m - 0.0m),
            >= 92_5000  => constant - 3.0m + (achievement - 92_5000m) / (97_5000m - 92_5000m) * (3.0m - 0.0m),
            >= 90_0000  => constant - 5.0m + (achievement - 90_0000m) / (92_5000m - 90_0000m) * (5.0m - 3.0m),
            >= 80_0000  => (constant - 5.0m) / 2 + (achievement - 80_0000m) / (90_0000m - 80_0000m) * ((constant - 5.0m) / 2),
            >= 50_0000  => (achievement - 50_0000m) / (80_0000m - 50_0000m) * (constant - 5m) / 2,
            _           => 0
        };
        return Math.Round(res, 2, MidpointRounding.ToZero);
    }

    /// <summary>
    ///     二分找下一个可以提高rating的达成率
    /// </summary>
    /// <param name="achievement">当前的达成率</param>
    /// <param name="constant">定数</param>
    /// <returns>达成率</returns>
    public static int NextRa(int achievement, decimal constant)
    {
        var l = achievement;
        var r = 100_9000;

        var currentRa = Ra(l, constant);

        while (l <= r)
        {
            var a = (l + r) / 2;
            if (Ra(a, constant) > currentRa)
            {
                r = a - 1;
            }
            else
            {
                l = a + 1;
            }
        }

        return Ra(l - 1, constant) > currentRa ? l - 1 : r + 1;
    }

    public override string MaxLevel()
    {
        return Levels[Constants.Select((c, i) => (c, i)).MaxBy(x => x.c).i];
    }

    public override Image GetCover()
    {
        return ResourceManager.GetCover(Id);
    }

    public override string GetImage()
    {
        var path = Path.Join(ResourceManager.TempPath, $"Detail.{Id}.{Hash()}.b64");
        return new CacheableText(path, () =>
        {
            var beatmaps = new List<object>();

            for (var i = 0; i < Constants.Count; i++)
            {
                beatmaps.Add(new
                {
                    LevelName   = LevelName[i],
                    MaxCombo    = MaxCombo[i],
                    LevelStr    = Levels[i],
                    Constant    = Constants[i],
                    ConstantOld = ConstantOld[i],
                    Charter     = Charters[i],
                    Bpm         = _bpms[i],
                    ChartName   = ChartName[i]
                });
            }

            var ctx = new WebContext();
            ctx.Put("SongData", new { Id, Title, Artist, Genre, Version, Beatmaps = beatmaps });
            return WebApi.ChunithmSong(ctx.Id).Result;
        }).Value;
    }

    [GeneratedRegex(@"[\d.]+")]
    private static partial Regex DoubleRegex();
}