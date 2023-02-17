using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using Microsoft.CSharp.RuntimeBinder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public class ChunithmSong : Song
{
    public readonly string Genre;
    public readonly string ReleaseDate;
    public readonly List<string> LevelName = new();
    public readonly List<long> MaxCombo = new();

    public ChunithmSong(dynamic o)
    {
        Id      = o.Id;
        Title   = o.Title;
        Artist  = o.Artist;
        Genre   = o.Genre;
        Version = o.Version;
        Bpm     = o.Beatmaps[0].Bpm;

        try
        {
            ReleaseDate = o.ReleaseDate;
        }
        catch (RuntimeBinderException)
        {
            ReleaseDate = "N/A";
        }

        foreach (var i in o.Beatmaps)
        {
            Constants.Add(i.Constant);
            Charters.Add(i.Charter);
            Levels.Add($"{(int)i.Constant}{(i.Constant - (int)i.Constant >= 0.5 ? "+" : "")}");
            LevelName.Add(i.LevelName);

            try
            {
                MaxCombo.Add(i.MaxCombo);
            }
            catch (RuntimeBinderException)
            {
                MaxCombo.Add(0);
            }
        }
    }

    public static double Ra(int achievement, double constant)
    {
        var maxRa = constant + 2.15;

        var res = achievement switch
        {
            >= 100_9000 => constant + 2.15,
            >= 100_7500 => constant + 2.0 + (achievement - 100_7500.0) / (100_9000.0 - 100_7500.0) * (2.15 - 2.0),
            >= 100_5000 => constant + 1.5 + (achievement - 100_5000.0) / (100_7500.0 - 100_5000.0) * (2.0 - 1.5),
            >= 100_0000 => constant + 1.0 + (achievement - 100_0000.0) / (100_5000.0 - 100_0000.0) * (1.5 - 1.0),
            >= 97_5000  => constant + 0.0 + (achievement - 97_5000.0) / (100_0000.0 - 97_5000.0) * (1.0 - 0.0),
            >= 92_5000  => constant - 3.0 + (achievement - 92_5000.0) / (97_5000.0 - 92_5000.0) * (3.0 - 0.0),
            >= 90_0000  => constant - 5.0 + (achievement - 90_0000.0) / (92_5000.0 - 90_0000.0) * (5.0 - 3.0),
            >= 80_0000  => (constant - 5.0) / 2 + (achievement - 80_0000.0) / (90_0000.0 - 80_0000.0) * ((constant - 5.0) / 2),
            >= 50_0000  => (achievement - 50_0000.0) / (80_0000.0 - 50_0000.0) * (constant - 5.0) / 2,
            _           => 0
        };
        return Math.Round(res, 2, MidpointRounding.ToZero);
    }

    /// <summary>
    /// 二分找下一个可以提高rating的达成率
    /// </summary>
    /// <param name="achievement">当前的达成率</param>
    /// <param name="constant">定数</param>
    /// <returns>达成率</returns>
    public static int NextRa(int achievement, double constant)
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

    /// <summary>
    /// 获取最小的达成率使得rating大于<paramref name="minRa"/>
    /// </summary>
    /// <param name="constant">定数</param>
    /// <param name="minRa">最小的Ra</param>
    /// <returns>达成率</returns>
    public static int NextRa(double constant, double minRa)
    {
        var a = 0;

        while (a < 100_9000)
        {
            a = NextRa(a, constant);

            if (Ra(a, constant) > minRa)
            {
                return a;
            }
        }

        return -1;
    }

    public override string MaxLevel()
    {
        return Levels[Constants.Select((c, i) => (c, i)).MaxBy(x => x.c).i];
    }

    public override Image GetCover()
    {
        return ResourceManager.GetCover(Id);
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

    public override string GetImage()
    {
        return new CacheableText(Path.Join(ResourceManager.TempPath, "Detail-") + Id + ".b64", () =>
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
                            .DrawLines(new Pen(Color.Gray, 1), new PointF(x, y - 1), new PointF(x + totalWidth, y - 1))
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
                DrawKeyValuePair("追加日期", ReleaseDate, x, y, w, h, background.Width);

                y += h;
                DrawKeyValuePair("版本", Version, x, y, w, h, background.Width);

                y = 3 * h;
                w = 100;
                DrawKeyValuePair("ID", Id.ToString(), 0, y, w, h, 3 * padding + 200, true, true);

                y += h;
                DrawKeyValuePair("BPM", Bpm.ToString("F1"), 0, y, w, h, 3 * padding + 200, true);

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
                    DrawCard(Constants[i].ToString("F1"), cardFontSize, Color.Black, bgColor2, w1, h, true);
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