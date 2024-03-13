using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ChunithmDraw
{
    public static Image? DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, ChunithmSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, int LevelIdx), ChunithmScore> scores)
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
                        new Point(x    + 12, y),
                        new Point(x, y + 12)
                    }));

                    // 难度指示器（小三角）
                    im.Mutate(ctx => ctx.Fill(ChunithmSong.LevelColor.Values.ElementAt(levelIdx), polygon));

                    // 跳过没有成绩的歌
                    if (!scores.ContainsKey((song.Id, levelIdx))) continue;

                    var score = scores[(song.Id, levelIdx)];

                    switch (score.Achievement)
                    {
                        // 边框
                        case >= 100_7500:
                            im.DrawImage(borderSss, x - borderWidth, y - borderWidth);
                            break;
                        case >= 100_0000:
                            im.DrawImage(borderSs, x - borderWidth, y - borderWidth);
                            break;
                        case >= 97_5000:
                            im.DrawImage(borderS, x - borderWidth, y - borderWidth);
                            break;
                    }

                    var achievement = score.Achievement.ToString();

                    var font = new Font(consolas, 30, FontStyle.Bold | FontStyle.Italic);

                    im.Mutate(ctx => ctx
                        .DrawLines(new Pen(Color.Black, 40), new PointF(x, y + height - 20),
                            new PointF(x                                              + height, y + height - 20))
                    );

                    var achString = (score.Achievement < 100_0000 ? "0" : "") + achievement;

                    var fontColor = score.Fc switch
                    {
                        "fullcombo"  => Color.LimeGreen,
                        "fullchain"  => Color.LawnGreen,
                        "fullchain2" => Color.Goldenrod,
                        "alljustice" => Color.Gold,
                        _            => Color.White
                    };
                    // 达成率
                    im.DrawText(achString, font, fontColor, x + 2, y + height - 30);

                    // rank 标志 (SSS+, SSS,...)
                    var rank = ResourceManager.GetImage($"rank_{score.Rank.ToLower()}.png").ResizeY(48);

                    im.DrawImage(rank, x + (height - rank.Width) / 2, y + (height - rank.Height - 30) / 2);
                }
            }

            _break: ;

            {
                var bg = new Image<Rgba32>(im.Width, im.Height + 70);

                var font = new Font(consolas, 45, FontStyle.Bold | FontStyle.Italic);

                var groupScores = group
                    .Select(tuple =>
                        scores.ContainsKey((tuple.Song.Id, tuple.LevelIdx))
                            ? scores[(tuple.Song.Id, tuple.LevelIdx)]
                            : null)
                    .ToList();

                // 根据 ap 和 fc 的状态确定 Key 的颜色
                var minFc = groupScores.Select(s => (s?.Fc ?? "") switch
                {
                    "fullcombo"  => 1,
                    "fullchain"  => 2,
                    "fullchain2" => 3,
                    "alljustice" => 4,
                    _            => 0
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

                if (minAch != 0)
                {
                    var imgRank = ResourceManager.GetImage($"rank_{ChunithmScore.GetRank(minAch)}.png").ResizeY(38);

                    bg.DrawImage(imgRank, (int)(padding - borderWidth + measure.Width + 10), padding);
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
}