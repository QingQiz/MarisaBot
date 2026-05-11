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
    ///     画汇总表
    /// </summary>
    public static Image? DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, MaiMaiSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, int LevelIdx), SongScore> scores)
    {
        const int column      = 8;
        const int height      = 120;
        const int padding     = 40;
        const int borderWidth = 10;

        var consolas = ImageDraw.GetFontFamily("Consolas", "Segoe UI", "Arial Unicode MS Font");

        var imList = new List<Image>();

        var borderSss = ResourceManager.GetImage("border_SSS.png");
        var borderSs  = ResourceManager.GetImage("border_SS.png");
        var borderS   = ResourceManager.GetImage("border_S.png");

        var rankImages = scores.Values
            .Select(x => x.Rank.ToLower())
            .Distinct()
            .ToDictionary(x => x, x => ResourceManager.GetImage($"rank_{x}.png").ResizeY(60));

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

                    var font = new Font(consolas, 35, FontStyle.Bold | FontStyle.Italic);

                    im.Mutate(ctx => ctx
                        .DrawLine(Color.Black, 40, new PointF(x, y + height - 20), new PointF(x + height, y + height - 20))
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
                    im.DrawText(ach1, font, fontColor, x + 2, y + height - 35);

                    font = new Font(consolas, 22, FontStyle.Bold | FontStyle.Italic);

                    // 达成率 小数部分 
                    im.DrawText("." + achievement[1], font, fontColor, x + 57, y + height - 26);

                    // rank 标志 (SSS+, SSS,...)
                    var rank = rankImages[score.Rank.ToLower()];

                    im.DrawImage(rank, x + (height - rank.Width) / 2, y + (height - rank.Height - 30) / 2);
                }
            }

            _break: ;

            {
                var bg = new Image<Rgba32>(im.Width, im.Height + 70);

                var font = new Font(consolas, 45, FontStyle.Bold | FontStyle.Italic);

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

                var imgRank = minAch > 0 ? rankImages[SongScore.CalcRank(minAch).ToLower()] : null;

                if (imgRank != null)
                {
                    bg.DrawImage(imgRank, padding - borderWidth + (int)measure.Width, padding / 2);
                }

                bg.DrawImage(im, 0, 70);

                imList.Add(bg);
            }

            if (imList.Count == 0)
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

    /// <summary>
    ///     画完成表（plate progress）。
    ///     与 DrawGroupedSong 的区别：按阈值过滤；达标者打对应维度图标且贴 SSS/SS/S 边框（基于
    ///     Achievement，与阈值维度无关）；未达者灰罩 + 50% 透明黑压暗。
    /// </summary>
    /// <param name="query">解析后的查询</param>
    /// <param name="pairs">已经按 selector + difficulty 过滤好的 (constant, levelIdx, song) 集合</param>
    /// <param name="scores">玩家成绩字典</param>
    /// <param name="title">顶部标题文本。null 时按 selector + threshold 自动拼。生产路径建议传用户原始命令。</param>
    /// <param name="iconMarkerHeight">FC/AP/FS/FDX 等图标的高度（按比例缩放宽度）。默认 75。</param>
    /// <param name="stampColor">已达成的 cell 上叠的"印章罩"颜色。默认黑色。</param>
    /// <param name="stampOpacity">印章罩透明度 [0,1]。默认 0.40。</param>
    public static Image DrawPlateProgress(
        PlateData.Query query,
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> pairs,
        IReadOnlyDictionary<(long SongId, int LevelIdx), SongScore> scores,
        string? title = null,
        int iconMarkerHeight = 75,
        Color? stampColor = null,
        float stampOpacity = 0.40f)
    {
        const int column            = 8;
        const int cellSize          = 120;
        const int padding           = 40;
        const int borderWidth       = 10;   // SSS/SS/S 边框图比 cell 大 10px 一圈，画在 cell 外侧
        const int groupHeaderHeight = 70;
        const int titleHeight       = 110;
        const int rankMarkerHeight  = 60;   // rank_*.png 是 200x89，用 ResizeY 保比例

        var consolas = ImageDraw.GetFontFamily("Consolas", "Segoe UI", "Microsoft YaHei", "Arial Unicode MS Font");

        // SSS/SS/S 边框图（140x140，正好 cellSize + 2*borderWidth）
        var borderSss = ResourceManager.GetImage("border_SSS.png");
        var borderSs  = ResourceManager.GetImage("border_SS.png");
        var borderS   = ResourceManager.GetImage("border_S.png");

        // 按 Level 字符串分组（"15+" / "15" / "14+" / ...），跨度内组按定数顺序由高到低
        var groups = pairs
            .OrderByDescending(t => t.Constant)
            .GroupBy(t => t.Song.Levels[t.LevelIdx])
            .OrderByDescending(g => LevelSortKey(g.Key))
            .ToList();

        bool IsPassed(MaiMaiSong song, int levelIdx, out SongScore? score)
        {
            if (!scores.TryGetValue((song.Id, levelIdx), out var s))
            {
                score = null;
                return false;
            }
            score = s;
            return PlateData.LevelOf(query.Threshold.Dim, s) >= query.Threshold.Level;
        }

        var total = pairs.Count;
        var done  = pairs.Count(t => IsPassed(t.Song, t.LevelIdx, out _));

        var groupImages = new List<Image>();

        foreach (var g in groups)
        {
            var charts = g.ToList();
            var rows   = (charts.Count + column - 1) / column;
            var cols   = rows > 1 ? column : charts.Count;

            var groupDone = charts.Count(t => IsPassed(t.Song, t.LevelIdx, out _));

            var groupImg = new Image<Rgba32>(
                cols * (cellSize + padding) + padding,
                rows * (cellSize + padding) + padding + groupHeaderHeight
            ).Clear(Color.White);

            // group header
            var headerFont = new Font(consolas, 40, FontStyle.Bold);
            groupImg.DrawText($"Lv {g.Key}    {groupDone}/{charts.Count}",
                headerFont, Color.FromRgb(40, 40, 40), padding, padding / 2);

            for (var j = 0; j < rows; j++)
            for (var i = 0; i < cols; i++)
            {
                var idx = j * cols + i;
                if (idx >= charts.Count) break;

                var (_, levelIdx, song) = charts[idx];
                var x = (i + 1) * padding + cellSize * i;
                var y = groupHeaderHeight + (j + 1) * padding + cellSize * j;

                var passed = IsPassed(song, levelIdx, out var score);

                var cover = ResourceManager.GetCover(song.Id).Resize(cellSize, cellSize);

                // 1. cover：原色显示。未达成的歌就这么呈现（让玩家看清是哪首没打）。
                groupImg.DrawImage(cover, x, y);

                // 2. SSS/SS/S 边框（基于实际 Achievement，跟阈值维度无关；未达阈值但 ach 高也带边框）
                if (score != null)
                {
                    var ach = score.Achievement;
                    Image? border = ach switch
                    {
                        >= 100 => borderSss,
                        >= 99  => borderSs,
                        >= 97  => borderS,
                        _      => null,
                    };
                    if (border != null)
                    {
                        groupImg.DrawImage(border, x - borderWidth, y - borderWidth);
                    }
                }

                // 3. 已达阈值 → 半透明白色"印章罩"压在 cover 上让 marker 突出，然后画 marker。
                //    未达不遮罩（玩家能看清原始封面，便于辨识"还没打过"的歌）。
                if (passed && score != null)
                {
                    var actualStamp = stampColor ?? Color.Black;
                    using var stamp = new Image<Rgba32>(cellSize, cellSize).Clear(actualStamp);
                    groupImg.DrawImage(stamp, x, y, stampOpacity);

                    var iconName = MarkerIconName(query.Threshold.Dim, score);
                    if (iconName != null)
                    {
                        var raw          = ResourceManager.GetImage(iconName);
                        var targetHeight = query.Threshold.Dim == PlateData.Dimension.Achievement
                            ? rankMarkerHeight : iconMarkerHeight;
                        var marker       = raw.ResizeY(targetHeight);
                        var mx           = x + (cellSize - marker.Width)  / 2;
                        var my           = y + (cellSize - marker.Height) / 2;
                        groupImg.DrawImage(marker, mx, my);
                    }
                }

                // 4. 难度三角（左上）— 最后画，确保不被边框 / 印章罩 / marker 遮挡
                var triangle = new Polygon(new LinearLineSegment(new PointF[]
                {
                    new Point(x, y),
                    new Point(x + 14, y),
                    new Point(x, y + 14),
                }));
                groupImg.Mutate(ctx => ctx.Fill(MaiMaiSong.LevelColor[levelIdx], triangle));
            }

            groupImages.Add(groupImg);
        }

        if (groupImages.Count == 0)
        {
            var empty = new Image<Rgba32>(800, 200).Clear(Color.White);
            empty.DrawText("（没有符合条件的曲目）",
                new Font(consolas, 40, FontStyle.Bold), Color.Gray, 40, 80);
            return empty;
        }

        // 顶部标题：默认用用户原始命令字符串，回退到 "<selector><threshold> 完成表"
        var titleFont = new Font(consolas, 50, FontStyle.Bold);
        var pct       = total == 0 ? 0 : done * 100.0 / total;
        var headLine  = title ?? $"{query.Selector.Display}{query.Threshold.DisplayName}{PlateData.CommandSuffix}";
        var titleText = $"{headLine}    {done}/{total}  ({pct:F1}%)";

        var width  = groupImages.Max(im => im.Width);
        var totalH = titleHeight + groupImages.Sum(im => im.Height);

        var res = new Image<Rgba32>(width, totalH).Clear(Color.White);
        res.DrawText(titleText, titleFont, Color.FromRgb(20, 20, 20), padding, 30);

        var yCur = titleHeight;
        foreach (var im in groupImages)
        {
            res.DrawImage(im, 0, yCur);
            yCur += im.Height;
        }

        return res;
    }

    /// <summary>把 maimai 难度字符串排序成整数（"15"=30, "15+"=31, "14"=28, "14+"=29 ...）。</summary>
    private static int LevelSortKey(string lv)
    {
        var plus    = lv.EndsWith('+');
        var numPart = plus ? lv[..^1] : lv;
        return int.TryParse(numPart, out var n) ? n * 2 + (plus ? 1 : 0) : 0;
    }

    /// <summary>按阈值维度返回应该叠在曲绘上的图标文件名（已含 .png）。</summary>
    private static string? MarkerIconName(PlateData.Dimension dim, SongScore score) => dim switch
    {
        PlateData.Dimension.Achievement => $"rank_{SongScore.CalcRank(score.Achievement)}.png",
        PlateData.Dimension.Fc          => string.IsNullOrEmpty(score.Fc) ? null : $"icon_{score.Fc}.png",
        PlateData.Dimension.Fs          => string.IsNullOrEmpty(score.Fs) ? null : $"icon_{score.Fs}.png",
        _                               => null,
    };

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
