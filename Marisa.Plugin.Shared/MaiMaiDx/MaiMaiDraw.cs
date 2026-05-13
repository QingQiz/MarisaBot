using Marisa.Plugin.Shared.Util;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public static class MaiMaiDraw
{
    /// <summary>
    ///     画歌曲信息
    /// </summary>
    /// <param name="song"></param>
    /// <returns></returns>
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

            DrawKeyValuePair("乐曲名", song.Title, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("演唱/作曲", song.Info.Artist, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("类别", song.Info.Genre, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("版本", song.Version, x, y, w, h, background.Width);

            y += h;
            DrawKeyValuePair("来源", song.Info.From, x, y, w, h, background.Width);

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

    /// <summary>
    ///     画汇总表（前端渲染）。把 grouped songs + scores 投到 WebContext，让
    ///     Marisa.Frontend 的 /maimai/summary 页面用 Puppeteer 截图。
    /// </summary>
    public static async Task<string> DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, MaiMaiSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, int LevelIdx), SongScore> scores,
        string title = "SUMMARY")
    {
        var ctx = new WebContext();
        ctx.Put("GroupedSongs", groupedSong.Select(g => new { g.Key, x = g.ToArray() }).ToArray());
        ctx.Put("Scores", scores);
        ctx.Put("Title", title);
        ctx.Put("Plate", null);
        return await WebApi.MaiMaiSummary(ctx.Id);
    }

    /// <summary>
    ///     画完成表（plate progress）。复用 /maimai/summary 前端页面 + plate 模式：
    ///     额外传 query 信息（dim/level/displayName/levelIdx），前端按维度筛达成 + 叠印章 + 居中 marker。
    /// </summary>
    public static async Task<string> DrawPlateProgress(
        PlateData.Query query,
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> pairs,
        IReadOnlyDictionary<(long SongId, int LevelIdx), SongScore> scores,
        string title)
    {
        // 按 Level 字符串分组（"15+"/"15"/"14+"...），跨度内按定数降序。
        var grouped = pairs
            .OrderByDescending(t => t.Constant)
            .GroupBy(t => t.Song.Levels[t.LevelIdx])
            .OrderByDescending(g => LevelSortKey(g.Key))
            .Select(g => new { Key = g.Key, x = g.ToArray() })
            .ToArray();

        var ctx = new WebContext();
        ctx.Put("GroupedSongs", grouped);
        ctx.Put("Scores", scores);
        ctx.Put("Title", title);
        ctx.Put("Plate", new
        {
            Dim         = query.Threshold.Dim.ToString(),  // "Achievement" / "Fc" / "Fs"
            Level       = query.Threshold.Level,
            DisplayName = query.Threshold.DisplayName,
        });
        return await WebApi.MaiMaiSummary(ctx.Id);
    }

    /// <summary>把 maimai 难度字符串排序成整数（"15"=30, "15+"=31, "14"=28, "14+"=29 ...）。</summary>
    private static int LevelSortKey(string lv)
    {
        var plus    = lv.EndsWith('+');
        var numPart = plus ? lv[..^1] : lv;
        return int.TryParse(numPart, out var n) ? n * 2 + (plus ? 1 : 0) : 0;
    }

    /// <summary>
    ///     画容错率表
    /// </summary>
    /// <param name="tap">单tap分</param>
    /// <param name="bonus">单绝赞bonus总分</param>
    /// <returns></returns>
    public static Image DrawFaultTable(double tap, double bonus)
    {
        var bm = ResourceManager.GetImage("fault-table.png");

        const int hW = 133, cW = 223;
        const int hH = 75,  cH = 75;

        var consolas = ImageDraw.GetFontFamily("Consolas", "Segoe UI", "Arial Unicode MS Font");

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

    public static Image? DrawRecommendCard(this DxRating rating, List<MaiMaiSong> songList)
    {
        SongScore?[]  songScores  = { null, null };
        MaiMaiSong?[] maiMaiSongs = { null, null };

        // 计算rating
        rating.NewScores.ForEach(s => s.Rating = s.Ra());
        rating.OldScores.ForEach(s => s.Rating = s.Ra());

        // 找到旧谱里能推的
        if (rating.OldScores.Any(s => s.Achievement < 100.5))
        {
            songScores[0] = rating.OldScores.Where(s => s.Achievement < 100.5).ToList().RandomTake();
        }

        // 找到新谱里能推的
        if (rating.NewScores.Any(s => s.Achievement < 100.5))
        {
            songScores[1] = rating.NewScores.Where(s => s.Achievement < 100.5).ToList().RandomTake();
        }

        // 找到不在b40里但定数不超过b40里最高定数+0.1的能推的旧谱
        long minSdRating = 0;
        if (rating.OldScores.Any())
        {
            minSdRating = rating.OldScores.Count < 35 ? 0 : rating.OldScores.Min(s => s.Rating);

            var playableSd = rating.OldScores.Select(s => s.Constant).Distinct().Max();

            var songListSd = songList
                .Where(s => s.Info.IsNew == false)
                .Where(s => rating.OldScores.All(ss => ss.Id != s.Id))
                .Where(s => s.Constants.Any(c => c <= playableSd + 0.1 && SongScore.Ra(100.5, c) > minSdRating))
                .ToList();

            if (songListSd.Any())
            {
                maiMaiSongs[0] = songListSd.RandomTake();
            }
        }

        // 同上，但是新谱
        long minDxRating = 0;
        if (rating.NewScores.Any())
        {
            minDxRating = rating.NewScores.Count < 15 ? 0 : rating.NewScores.Min(s => s.Rating);

            var playableDx = rating.NewScores.Select(s => s.Constant).Distinct().Max();

            var songListDx = songList
                .Where(s => s.Info.IsNew)
                .Where(s => rating.NewScores.All(ss => ss.Id != s.Id))
                .Where(s => s.Constants.Any(c => c <= playableDx + 0.1 && SongScore.Ra(100.5, c) > minDxRating))
                .ToList();

            if (songListDx.Any())
            {
                maiMaiSongs[1] = songListDx.RandomTake();
            }
        }

        Image GetRecommendCard1(SongScore score)
        {
            const int padding = 20;

            var consolas = ImageDraw.GetFontFamily("Consolas", "Segoe UI", "Arial Unicode MS Font");

            var cover = ResourceManager.GetCover(score.Id);
            var bg    = new Image<Rgba32>(800, cover.Height + padding * 2);

            var song = songList.First(s => s.Id == score.Id);

            var titleFont = new Font(consolas, 37, FontStyle.Bold);
            var font      = new Font(consolas, 22);
            var fontS     = new Font(consolas, 14, FontStyle.Italic | FontStyle.Bold);
            var fontB     = new Font(consolas, 22, FontStyle.Bold);

            var y = (bg.Height - cover.Height) / 2;
            var x = padding + cover.Width + padding;

            bg.Mutate(i => i.Fill(Color.White).DrawImage(cover, padding, y));

            var nextA = score.NextRa();
            var nextR = SongScore.Ra(nextA, score.Constant);

            var lvStr   = $"{MaiMaiSong.LevelNameAll[score.LevelIdx]}: {score.Constant}";
            var measure = lvStr.Measure(fontS);
            bg.DrawText(lvStr, fontS, MaiMaiSong.LevelColor[score.LevelIdx], bg.Width - padding - measure.Width, bg.Height - padding - measure.Height);

            var sd = new StringDrawer(5);

            sd.Add(
                (song.Title, titleFont, Color.Black),
                ($"\n当前达成率为: {score.Achievement:F4}\n", font, Color.Black),
                ($"   Rating为: {score.Rating}\n", font, Color.Black),
                ($"推分到达成率: {nextA:F4}", font, Color.Black),
                ($"(+{nextA - score.Achievement:F4})\n", font, Color.SpringGreen),
                ($"   Rating为: {nextR}", font, Color.Black),
                ($"(+{nextR - score.Rating})\n", font, Color.SpringGreen),
                ("可推", fontB, Color.Black),
                ($" {nextR - score.Rating} ", fontB, Color.Red),
                ("分", fontB, Color.Black),
                ($"({(song.Info.IsNew ? "新谱" : "旧谱")})", fontB, Color.Gray)
            );

            sd.Draw(bg, x, y);

            return bg;
        }

        Image GetRecommendCard2(MaiMaiSong song, long minRating)
        {
            const int padding = 20;

            var consolas = ImageDraw.GetFontFamily("Consolas", "Segoe UI", "Arial Unicode MS Font");

            var cover = ResourceManager.GetCover(song.Id);
            var bg    = new Image<Rgba32>(800, cover.Height + padding * 2);

            var titleFont = new Font(consolas, 37, FontStyle.Bold);
            var font      = new Font(consolas, 22);
            var fontS     = new Font(consolas, 14, FontStyle.Italic | FontStyle.Bold);
            var fontB     = new Font(consolas, 22, FontStyle.Bold);

            var y = (bg.Height - cover.Height) / 2;
            var x = padding + cover.Width + padding;

            bg.Mutate(i => i.Fill(Color.White).DrawImage(cover, padding, y));

            var constantIndex = song.Constants.FindIndex(c => SongScore.NextAchievement(c, minRating) > 0);
            var constant      = song.Constants[constantIndex];

            var nextA = SongScore.NextAchievement(constant, minRating);
            var nextR = SongScore.Ra(nextA, constant);

            var lvStr   = $"{MaiMaiSong.LevelNameAll[constantIndex]}: {constant}";
            var measure = lvStr.Measure(fontS);

            bg.DrawText(lvStr, fontS, MaiMaiSong.LevelColor[constantIndex], bg.Width - padding - measure.Width, bg.Height - padding - measure.Height);

            var sd = new StringDrawer(9);

            sd.Add(
                (song.Title, titleFont, Color.Black),
                ($"\n歌曲不在你的B50里,地板: {minRating}\n", font, Color.Peru),
                ($"推分到达成率: {nextA:F4}\n", font, Color.Black),
                ($"   Rating为: {nextR}", font, Color.Black),
                ($"(+{nextR - minRating})\n", font, Color.SpringGreen),
                ("可推", fontB, Color.Black),
                ($" {nextR - minRating} ", fontB, Color.Red),
                ("分", fontB, Color.Black),
                ($"({(song.Info.IsNew ? "新谱" : "旧谱")})", fontB, Color.Gray)
            );
            sd.Draw(bg, x, y);

            return bg;
        }

        Image?[] card = { null, null, null, null };

        // 获取推荐卡片
        card[0] = songScores[0] == null ? null : GetRecommendCard1(songScores[0]!);
        card[1] = maiMaiSongs[0] == null ? null : GetRecommendCard2(maiMaiSongs[0]!, minSdRating);
        card[2] = songScores[1] == null ? null : GetRecommendCard1(songScores[1]!);
        card[3] = maiMaiSongs[1] == null ? null : GetRecommendCard2(maiMaiSongs[1]!, minDxRating);

        const int cardHeight = 240;
        const int cardWidth  = 800;

        var cnt = card.Count(x => x != null);

        if (cnt == 0)
        {
            return null;
        }

        // 把推荐卡片拼起来
        var bg = new Image<Rgba32>(cardWidth, cnt * cardHeight);

        var y = 0;

        foreach (var c in card)
        {
            if (c == null) continue;

            bg.DrawImage(c, 0, y);
            y += cardHeight;

            var y1 = y;
            bg.Mutate(i => i
                .DrawLine(Color.DarkGray, 2, new Point(0, y1 - 1), new Point(cardWidth, y1 - 1))
            );
        }

        bg.Mutate(i => i
            .DrawLine(Color.DarkGray, 2, new Point(0, 0), new Point(cardWidth, 0))
        );

        return bg;
    }
}
