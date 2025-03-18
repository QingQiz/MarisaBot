using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;
using Microsoft.CSharp.RuntimeBinder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public class ChunithmSong : Song
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

    public new readonly ReadOnlyMemory<char> Bpm;
    public readonly List<string> ChartName = [];
    public readonly string Genre;
    /// <summary>
    ///     BASIC, ADVANCED, EXPERT, MASTER, ULTIMA, ...
    /// </summary>
    public readonly List<string> LevelName = [];
    public readonly List<long> MaxCombo = [];

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
                Bpm     = ((string)o.basic_info.bpm.ToString()).AsMemory();

                for (var i = 0; i < o.level.Count; i++)
                {
                    Constants.Add(o.ds[i]);
                    Charters.Add(o.charts[i].charter);
                    Levels.Add(o.level[i]);
                    LevelName.Add(o.level[i]);
                    MaxCombo.Add(o.charts[i].combo);
                    ChartName.Add("");
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

                foreach (var chart in new[] { o.charts.basic, o.charts.advanced, o.charts.expert, o.charts.master, o.charts.ultima, o.charts.worldsend })
                {
                    if (!chart.enabled) continue;

                    Constants.Add(chart.constant);
                    Charters.Add(chart.charter);
                    Levels.Add(chart.level);
                    LevelName.Add(chart.level);
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

                var bpms = new List<string>();

                foreach (var i in o.Beatmaps)
                {
                    bpms.Add(i.Bpm);
                }

                Bpm = bpms.MaxBy(x => x.Length).AsMemory();

                foreach (var i in o.Beatmaps)
                {
                    Constants.Add(i.Constant);
                    Charters.Add(i.Charter);
                    Levels.Add(i.LevelStr);
                    LevelName.Add(i.LevelName);
                    ChartName.Add(i.ChartName);

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

    public ReadOnlyMemory<char> BpmNorm => Bpm.Split(' ').First();

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
            const int cardFontSize = 31;
            const int padding      = 10;

            Image GetSongInfoCard()
            {
                var       bgColor1 = Color.FromRgb(237, 237, 237);
                var       bgColor2 = Color.FromRgb(250, 250, 250);
                const int h        = 80;

                var cover = ResourceManager.GetCover(Id);

                var background = new Image<Rgba32>(1000, h * 5);

                void DrawKeyValuePair(
                    string key, string value, int x, int y, int keyWidth, int height, int totalWidth,
                    bool center = false, bool overline = false)
                {
                    var card1 = ImageDraw.GetStringCard(key, cardFontSize, Color.Black, bgColor1, keyWidth, height, center: true);
                    var card2 = ImageDraw.GetStringCard(value, cardFontSize, Color.Black, bgColor2, totalWidth - (x + keyWidth), height, center: center);

                    if (overline)
                    {
                        background.Mutate(i => i
                            .DrawLine(Color.Gray, 1, new PointF(x, y - 1), new PointF(x + totalWidth, y - 1))
                        );
                    }

                    background.Mutate(i => i
                        .DrawImage(card1, x, y)
                        .DrawImage(card2, x + keyWidth, y)
                    );
                }

                // ReSharper disable once ConvertToConstant.Local
                var x = 3 * padding + 200;
                var y = 0;
                var w = 200;

                background.Mutate(i => i.DrawImage(cover, padding, padding));

                DrawKeyValuePair("乐曲名", Title, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("演唱/作曲", Artist, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("类别", Genre, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("版本", Version, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("BPM FULL", Bpm.ToString(), x, y, w, h, background.Width);

                y = 3 * h;
                w = 100;
                DrawKeyValuePair("ID", Id.ToString(), 0, y, w, h, 3 * padding + 200, true, true);

                y += h;
                DrawKeyValuePair("BPM", BpmNorm.ToString(), 0, y, w, h, 3 * padding + 200, true);

                return background;
            }

            Image GetChartInfoCard()
            {
                var bgColor1 = Color.FromRgb(237, 237, 237);
                var bgColor2 = Color.FromRgb(250, 250, 250);

                const int h  = 80;
                const int w1 = 100;

                var background = new Image<Rgba32>(1000, h * (Levels.Count + 1));

                var x = 0;
                var y = 0;

                void DrawCard(string txt, int fontSize, Color fontColor, Color bgColor, int width, int height, bool center)
                {
                    background.Mutate(i => i.DrawImage(ImageDraw.GetStringCard(txt, fontSize, fontColor, bgColor, width, height, center: center), x, y));
                }

                DrawCard("难度", cardFontSize, Color.Black, bgColor1, w1, h, true);
                x += w1;
                DrawCard("定数", cardFontSize, Color.Black, bgColor1, w1, h, true);
                x += w1;
                DrawCard("Combo", cardFontSize, Color.Black, bgColor1, w1, h, true);
                x += w1;
                DrawCard("谱师", cardFontSize, Color.Black, bgColor1, background.Width - x, h, true);

                y += h;
                x =  0;


                for (var i = 0; i < Levels.Count; i++)
                {
                    var c = LevelColor[LevelName[i]];

                    DrawCard(Levels[i], cardFontSize, c.SelectFontColor(), c, w1, h, true);
                    x += w1;
                    DrawCard(Constants[i] == 0 ? "-" : Constants[i].ToString("F1"), cardFontSize, Color.Black, bgColor2, w1, h, true);
                    x += w1;
                    DrawCard(MaxCombo[i].ToString(), cardFontSize, Color.Black, bgColor2, w1, h, true);
                    x += w1;
                    DrawCard(Charters[i], cardFontSize, Color.Black, bgColor2, background.Width - x, h, true);

                    y += h;
                    x =  0;
                }

                return background;
            }

            var cd1 = GetSongInfoCard();
            var cd2 = GetChartInfoCard();

            var background = new Image<Rgba32>(cd1.Width + padding * 2, cd1.Height + cd2.Height + padding * 4);

            background.Mutate(i => i
                .Fill(Color.FromRgb(250, 250, 250))
                .DrawImage(cd1, padding, padding)
                .DrawImage(cd2, padding, 3 * padding + cd1.Height)
            );

            return background.ToB64();
        }).Value;
    }
}