using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public static class MaiMaiDraw
{
    public static Image Draw(this MaiMaiSong song)
    {
        const int cardFontSize = 31;
        const int padding      = 10;

        Image GetSongInfoCard()
        {
            var       bgColor1 = Color.FromRgb(237, 237, 237);
            var       bgColor2 = Color.FromRgb(250, 250, 250);
            const int h        = 80;

            var cover = ResourceManager.GetCover(song.Id);

            var background = new Image<Rgba32>(song.Type == "DX" ? 1200 : 1000, h * 5);

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

            DrawKeyValuePair("乐曲名", song.Title, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("演唱/作曲", song.Info.Artist, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("类别", song.Info.Genre, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("追加日期", song.Info.ReleaseDate, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("版本", song.Info.From, x, y, w, h, background.Width);

            y = 3 * h;
            w = 100;
            DrawKeyValuePair("ID", song.Id.ToString(), 0, y, w, h, 3 * padding + 200, true, true);

            y += h;
            DrawKeyValuePair("BPM", song.Info.Bpm.ToString(), 0, y, w, h, 3 * padding + 200, true);

            return background;
        }

        Image GetChartInfoCard()
        {
            var bgColor1 = Color.FromRgb(237, 237, 237);
            var bgColor2 = Color.FromRgb(250, 250, 250);

            const int h = 80;
            const int w = 110;

            var background = new Image<Rgba32>(song.Type == "DX" ? 1200 : 1000, h * (song.Levels.Count + 1));

            var x = 0;
            var y = 0;

            void DrawCard(string txt, int fontSize, Color fontColor, Color bgColor, int width, int height, bool center)
            {
                background.Mutate(i => i.DrawImage(ImageDraw.GetStringCard(txt, fontSize, fontColor, bgColor, width, height, center: center), x, y));
            }

            DrawCard("难度", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("定数", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("COMBO", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("TAP", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("HOLD", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("SLIDE", cardFontSize, Color.Black, bgColor1, w, h, true);

            if (song.Type == "DX")
            {
                x += w;
                DrawCard("TOUCH", cardFontSize, Color.Black, bgColor1, w, h, true);
            }

            x += w;
            DrawCard("BREAK", cardFontSize, Color.Black, bgColor1, w, h, true);
            x += w;
            DrawCard("谱师", cardFontSize, Color.Black, bgColor1, background.Width - x, h, true);

            y += h;
            x =  0;

            for (var i = 0; i < song.Levels.Count; i++)
            {
                DrawCard(song.Levels[i], cardFontSize, Color.Black, MaiMaiSong.LevelColor[i], w, h, true);
                x += w;
                DrawCard(song.Constants[i].ToString("F1").Trim('0').Trim('.'), cardFontSize, Color.Black, bgColor2, w, h, true);
                x += w;
                DrawCard(song.Charts[i].Notes.Sum().ToString(), cardFontSize, Color.Black, bgColor2, w, h, true);

                foreach (var c in song.Charts[i].Notes)
                {
                    x += w;
                    DrawCard(c.ToString(), cardFontSize, Color.Black, bgColor2, w, h, true);
                }

                x += w;
                DrawCard(song.Charters[i], cardFontSize, Color.Black, bgColor2, background.Width - x, h, true);

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

        return background;
    }

    public static Image? DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, MaiMaiSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, long LevelIdx), SongScore> scores)
    {
        const int column      = 8;
        const int height      = 120;
        const int padding     = 40;
        const int borderWidth = 10;

        var consolas = SystemFonts.Get("Consolas");

        var imList = new List<Image>();

        var borderSss = ResourceManager.GetImage("border_SSS.png");
        var borderSs  = ResourceManager.GetImage("border_SS.png");
        var borderS   = ResourceManager.GetImage("border_S.png");

        foreach (var group in groupedSong)
        {
            var key = group.Key;
            var value = group
                .Select(x => (x.LevelIdx, x.Song))
                // 先按白紫红黄绿排
                .OrderByDescending(song => song.LevelIdx)
                // 再按 ID 排
                .ThenByDescending(song => song.Song.Id)
                .ToList();

            if (value.Count == 0) continue;

            var rows = (value.Count + column - 1) / column;
            var cols = rows > 1 ? column : value.Count;

            var im = new Image<Rgba32>(cols * (height + padding) + padding, rows * (height + padding) + padding);

            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < cols; i++)
                {
                    var idx = j * cols + i;
                    if (idx >= value.Count) goto _break;

                    var (levelIdx, song) = value[idx];

                    var x     = (i + 1) * padding + height * i;
                    var y     = (j + 1) * padding + height * j;
                    var cover = ResourceManager.GetCover(song.Id).Resize(height, height);

                    im.DrawImage(cover, x, y);

                    var polygon = new Polygon(new LinearLineSegment(new PointF[]
                    {
                        new Point(x, y),
                        new Point(x + 12, y),
                        new Point(x, y + 12)
                    }));

                    // 难度指示器（小三角）
                    im.Mutate(ctx => ctx.Fill(MaiMaiSong.LevelColor[levelIdx], polygon));

                    // 跳过没有成绩的歌
                    if (!scores.ContainsKey((song.Id, levelIdx))) continue;

                    var score = scores[(song.Id, levelIdx)];

                    switch (score.Achievement)
                    {
                        // 边框
                        case >= 100:
                            im.DrawImage(borderSss, x - borderWidth, y - borderWidth);
                            break;
                        case >= 99:
                            im.DrawImage(borderSs, x - borderWidth, y - borderWidth);
                            break;
                        case >= 97:
                            im.DrawImage(borderS, x - borderWidth, y - borderWidth);
                            break;
                    }

                    var achievement = score.Achievement.ToString("F4").Split('.');

                    var font = new Font(consolas, 24, FontStyle.Bold | FontStyle.Italic);

                    im.Mutate(ctx => ctx
                        .DrawLines(new Pen(Color.Black, 40), new PointF(x, y + height - 20), new PointF(x + height, y + height - 20))
                    );

                    var ach1 = (score.Achievement < 100 ? "0" : "") + achievement[0];

                    var fontColor = score.Fc switch
                    {
                        "fc"  => Color.LimeGreen,
                        "fcp" => Color.LawnGreen,
                        "ap"  => Color.Goldenrod,
                        "app" => Color.Gold,
                        _     => Color.White
                    };
                    // 达成率 整数部分
                    im.DrawText(ach1, font, fontColor, x, y + height - 40);

                    font = new Font(consolas, 14, FontStyle.Bold | FontStyle.Italic);

                    // 达成率 小数部分 
                    im.DrawText("." + achievement[1], font, fontColor, x + 55, y + height - 28);

                    // rank 标志 (SSS+, SSS,...)
                    var rank = ResourceManager.GetImage($"rank_{score.Rank.ToLower()}.png");

                    im.DrawImage(rank, x + (height - rank.Width) / 2, y + (height - rank.Height - 30) / 2);
                }
            }

            _break: ;

            {
                var bg = new Image<Rgba32>(im.Width, im.Height + 70);

                var font = new Font(consolas, 35, FontStyle.Bold | FontStyle.Italic);

                var groupScores = group
                    .Select(tuple => scores.ContainsKey((tuple.Song.Id, tuple.LevelIdx)) ? scores[(tuple.Song.Id, tuple.LevelIdx)] : null)
                    .ToList();

                // 根据 ap 和 fc 的状态确定 Key 的颜色
                var minFc = groupScores.Select(s => (s?.Fc ?? "") switch
                {
                    "fc"  => 1,
                    "fcp" => 2,
                    "ap"  => 3,
                    "app" => 4,
                    _     => 0
                }).Min();

                var fontColor = minFc switch
                {
                    1 => Color.LimeGreen,
                    2 => Color.LawnGreen,
                    3 => Color.Goldenrod,
                    4 => Color.Gold,
                    _ => Color.Black
                };

                bg.DrawText(key, font, fontColor, padding - borderWidth, padding);

                var measure = key.Measure(font);

                // 如果全 sss/ss/s 则标记出来
                var minAch = groupScores.Min(x => x?.Achievement ?? 0);

                var imgRank = minAch switch
                {
                    >= 100.5 => ResourceManager.GetImage("rank_sssp.png"),
                    >= 100   => ResourceManager.GetImage("rank_sss.png"),
                    >= 99.5  => ResourceManager.GetImage("rank_ssp.png"),
                    >= 99    => ResourceManager.GetImage("rank_ss.png"),
                    >= 98    => ResourceManager.GetImage("rank_sp.png"),
                    >= 97    => ResourceManager.GetImage("rank_s.png"),
                    _        => null
                };

                if (imgRank != null)
                {
                    imgRank = imgRank.Resize(0.8);
                    bg.DrawImage(imgRank, (int)(padding - borderWidth + measure.Width), (int)(padding + (measure.Height - imgRank.Height) / 2));
                }

                bg.DrawImage(im, 0, 70);

                imList.Add(bg);
            }

            if (!imList.Any())
            {
                return null;
            }
        }

        {
            var res = new Image<Rgba32>(imList.Max(im => im.Width), imList.Sum(im => im.Height));

            res.Clear(Color.White);

            var y = 0;
            foreach (var im in imList)
            {
                res.DrawImage(im, 0, y);
                y += im.Height;
            }

            return res;
        }
    }

    public static Image DrawFaultTable(double tap, double bonus)
    {
        var bm = ResourceManager.GetImage("fault-table.png");

        const int hW = 133, cW = 223;
        const int hH = 75,  cH = 75;

        var consolas = SystemFonts.Get("Consolas");

        void DrawString(string s, Font f, int x, int y)
        {
            var m        = s.Measure(f);
            var paddingX = (cW * (x == 3 ? 2 : 1) - m.Width) / 2;
            var paddingY = (cH - m.Height) / 2;

            bm.DrawText(s, f, Color.Black, hW + x * cW + paddingX, hH + y * cH + paddingY);
        }

        var fontS = new Font(consolas, 30, FontStyle.Regular);
        var fontL = new Font(consolas, 32, FontStyle.Regular);

        // perfect
        DrawString($"{0.25 * bonus:F4} / {0.5 * bonus:F4}", fontL, 3, 0);
        // great
        DrawString($"{0.2 * tap:F4}", fontL, 0, 1);
        DrawString($"{0.4 * tap:F4}", fontL, 1, 1);
        DrawString($"{0.6 * tap:F4}", fontL, 2, 1);
        DrawString($"{1.0 * tap + 0.6 * bonus:F4} / {2 * tap + 0.6 * bonus:F4} / {2.5 * tap + 0.6 * bonus:F4}", fontS, 3, 1);
        // good
        DrawString($"{0.5 * tap:F4}", fontL, 0, 2);
        DrawString($"{1.0 * tap:F4}", fontL, 1, 2);
        DrawString($"{1.5 * tap:F4}", fontL, 2, 2);
        DrawString($"{3.0 * tap + 0.7 * bonus:F4}", fontL, 3, 2);
        // miss
        DrawString($"{1.0 * tap:F4}", fontL, 0, 3);
        DrawString($"{2.0 * tap:F4}", fontL, 1, 3);
        DrawString($"{3.0 * tap:F4}", fontL, 2, 3);
        DrawString($"{5.0 * tap + 1.0 * bonus:F4}", fontL, 3, 3);

        return bm;
    }
}