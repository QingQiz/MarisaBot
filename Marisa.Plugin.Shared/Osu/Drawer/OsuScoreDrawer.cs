using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuScoreDrawer
{
    private static async Task<Image> GetCover(this OsuScore score)
    {
        var uri = score.Beatmapset.Covers.Cover2X;
        return await OsuDrawerCommon.GetCacheOrDownload(uri, "jpg");
    }

    private const int ImageWidth = 2000;
    private const int MarginX = 100;

    private static FontFamily _fontExo2 = SystemFonts.Get("Exo 2");
    private static FontFamily _fontYaHei = SystemFonts.Get("Microsoft YaHei");
    private static FontFamily _fontIcon = SystemFonts.Get("Segoe UI Symbol");
    private static readonly Color BgColor = Color.FromRgb(46, 53, 56);


    public static async Task<Image> GetImage(this OsuScore score, OsuUserInfo info)
    {
        var scoreHeader = GetScoreHeader(score.Beatmap, score.Beatmapset);

        // 封面
        var cover = (await score.GetCover()).ResizeX(2000);
        var grade = new Image<Rgba32>(ImageWidth, Math.Max(cover.Height, 500));
        grade.DrawImage(cover, 0, 0).Clear(Color.FromRgba(0, 0, 0, 175));

        // 成绩的 rank 轴
        var iconBar = IconBar(score.Rank);
        grade.DrawImageVCenter(iconBar, MarginX);

        // 成绩环
        const int ringMarginLeft = 50;

        var ring = GetAccRing(score.Rank, score.Accuracy, score.ModeInt).Resize(1.2);
        grade.DrawImageVCenter(ring, MarginX + iconBar.Width + ringMarginLeft);

        // 成绩数值
        const int gradeCardMarginLeft = 50;

        var maxWidth = ImageWidth - MarginX - iconBar.Width - ringMarginLeft - ring.Width - gradeCardMarginLeft - MarginX;

        grade.DrawImageVCenter(GetGradeDetail(maxWidth, score.Score, score.Mods, score.CreatedAt),
            MarginX + iconBar.Width + ringMarginLeft + ring.Width + gradeCardMarginLeft);

        // TODO 歌曲详情

        // 玩家卡片
        var userCard = (await info.GetMiniCard()).ResizeX((int)((ImageWidth - MarginX * 2) * 0.4));

        var sta = (await GetScoreSta(score)).ResizeX(ImageWidth - MarginX * 2 - userCard.Width - MarginX);

        // 拼起来
        const int userCardVGap = 40;

        var res = new Image<Rgba32>(ImageWidth, scoreHeader.Height + grade.Height + userCard.Height + userCardVGap * 2);

        res.Mutate(i => i
            .Fill(BgColor)
            .DrawImage(scoreHeader, 0, 0)
            .DrawImage(grade, 0, scoreHeader.Height)
            .DrawImage(userCard, MarginX, scoreHeader.Height + grade.Height + userCardVGap)
            .DrawImage(sta, res.Width - sta.Width - MarginX, scoreHeader.Height + grade.Height + userCardVGap)
        );

        return res;
    }

    private static async Task<Image> GetScoreSta(OsuScore score)
    {
        const int staCardGap  = 2;
        const int staCardVGap = 60;

        var pp = score.Pp?.ToString("F2") ??
            (await PerformanceCalculator.GetPerformance(score)).ToString("F2");

        var c1 = GetKeyValuePair("准确率", $"{score.Accuracy * 100:F2}%", (ImageWidth - staCardGap * 2) / 3);
        var c2 = GetKeyValuePair("最大连击", $"{score.MaxCombo:N0}x", (ImageWidth - staCardGap * 2) / 3);
        var c3 = GetKeyValuePair("PP", pp, (ImageWidth - staCardGap * 2) / 3);

        var cards = new List<Image>();

        switch (score.ModeInt)
        {
            case 3:
            {
                const int width = (ImageWidth - staCardGap * 5) / 6;

                cards.AddRange(new[]
                {
                    GetKeyValuePair("MAX", $"{score.Statistics.CountGeki:N0}", width),
                    GetKeyValuePair("300", $"{score.Statistics.Count300:N0}", width),
                    GetKeyValuePair("200", $"{score.Statistics.CountKatu:N0}", width),
                    GetKeyValuePair("100", $"{score.Statistics.Count100:N0}", width),
                    GetKeyValuePair("50", $"{score.Statistics.Count50:N0}", width),
                    GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", width),
                });
                break;
            }
            case 0:
            {
                const int width = (ImageWidth - staCardGap * 3) / 4;

                cards.AddRange(new[]
                {
                    GetKeyValuePair("300", $"{score.Statistics.Count300:N0}", width),
                    GetKeyValuePair("100", $"{score.Statistics.Count100:N0}", width),
                    GetKeyValuePair("50", $"{score.Statistics.Count50:N0}", width),
                    GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", width),
                });
                break;
            }
            case 1:
            {
                const int width = (ImageWidth - staCardGap * 2) / 3;

                cards.AddRange(new[]
                {
                    GetKeyValuePair("GREAT", $"{score.Statistics.Count300:N0}", width),
                    GetKeyValuePair("GOOD", $"{score.Statistics.Count100:N0}", width),
                    GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", width),
                });
                break;
            }
            case 2:
            {
                const int width = (ImageWidth - staCardGap * 3) / 4;

                cards.AddRange(new[]
                {
                    GetKeyValuePair("FRUITS", $"{score.Statistics.Count300:N0}", width),
                    GetKeyValuePair("TICKS", $"{score.Statistics.Count100:N0}", width),
                    GetKeyValuePair("DRP MISS", $"{score.Statistics.CountKatu:N0}", width),
                    GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", width),
                });
                break;
            }
        }

        var sta = new Image<Rgba32>(ImageWidth, c1.Height + cards[0].Height + staCardVGap).Clear(BgColor);

        sta.DrawImage(c1, 0, 0);
        sta.DrawImage(c2, c1.Width + staCardGap, 0);
        sta.DrawImage(c3, (c1.Width + staCardGap) * 2, 0);

        for (var i = 0; i < cards.Count; i++)
        {
            sta.DrawImage(cards[i], (cards[0].Width + staCardGap) * i, c1.Height + staCardVGap);
        }

        return sta;
    }

    private static Image<Rgba32> GetKeyValuePair(string key, string value, int width)
    {
        const int gap = 10;

        var fKey   = _fontYaHei.CreateFont(60);
        var fValue = _fontExo2.CreateFont(100);

        var mKey   = key.MeasureWithSpace(fKey);
        var mValue = value.MeasureWithSpace(fValue);

        var imageHeader = new Image<Rgba32>(width, (int)mKey.Height).Clear(Color.ParseHex("#171a1c"));

        imageHeader.DrawTextCenter(key, fKey, Color.White);

        var image = new Image<Rgba32>(width, (int)(imageHeader.Height + mValue.Height + gap));

        image.DrawImage(imageHeader.RoundCorners(imageHeader.Height / 2), 0, 0);
        image.DrawTextHCenter(value, fValue, Color.White, imageHeader.Height + gap);

        return image;
    }

    private static Image GetStatusIcon(string status)
    {
        status = status.ToLower();

        var im = new Image<Rgba32>(60, 60);

        switch (status)
        {
            case "ranked":
            {
                var font = _fontExo2.CreateFont(60);

                im.DrawTextCenter("❰❰", font, Color.ParseHex("#64c6f5"), withSpace: false);
                im.Mutate(i => i.Rotate(90));
                break;
            }
            case "loved":
            {
                var font = _fontIcon.CreateFont(70, FontStyle.Regular);

                im.DrawTextCenter("♥", font, Color.FromRgb(255, 102, 171), withSpace: false);
                break;
            }
            case "approved" or "qualified":
            {
                var font = _fontYaHei.CreateFont(50);

                im.DrawTextCenter("✔", font, Color.Black, withSpace: false);
                break;
            }
            default:
            {
                var font = _fontYaHei.CreateFont(45);

                im.DrawTextCenter("❔", font, Color.Black, withSpace: false);
                break;
            }
        }

        return im;
    }

    private static Image<Rgba32> GetGradeDetail(int maxWidth, long grade, string[] mods, DateTimeOffset time)
    {
        var gradeText = grade.ToString("N0");
        var timeText  = (time + TimeSpan.FromHours(8)).ToString("// yyyy-MM-dd hh:mm:ss");

        var card = new Image<Rgba32>(maxWidth, 500);

        var font1 = _fontExo2.CreateFont(160);
        var font3 = _fontExo2.CreateFont(40);

        var icons = mods.Select(GetModIcon);

        int y1, y2, y3;

        if (mods.Any())
        {
            y1 = 70;
            y2 = 130;
            y3 = 330;
        }
        else
        {
            y1 = 0;
            y2 = 100;
            y3 = 300;
        }

        const int modIconWidth = 110;

        var gap = Math.Min(10, (float)(card.Width - mods.Length * modIconWidth) / (mods.Length - 1));

        var x = 0.0;
        foreach (var i in icons)
        {
            var draw = i.ResizeX(modIconWidth);

            card.DrawImage(draw, (int)x, y1);
            x += draw.Width + gap;
        }

        card.DrawText(gradeText, font1, Color.White, 0, y2);
        card.DrawText(timeText, font3, Color.White, 10, y3);

        return card;
    }

    private static Image<Rgba32> IconBar(string scoreRank)
    {
        var rankIdx = new List<string> { "d", "c", "b", "a", "s", "ss" };

        const int iconHeight = 40;
        const int iconWidth  = iconHeight * 2;
        const int iconGap    = iconHeight / 2;

        var rank = scoreRank.ToLower();
        var idx  = rankIdx.IndexOf(rank.Replace("h", ""));

        var iconBar = new Image<Rgba32>(iconWidth, iconHeight * rankIdx.Count + iconGap * (rankIdx.Count - 1));

        var drawY = iconBar.Height - iconHeight;

        for (var i = 0; i < rankIdx.Count; i++)
        {
            var s = rankIdx[i];

            if (i < idx)
            {
                iconBar.DrawImage(OsuDrawerCommon.GetRankIcon(s).Resize(iconWidth, iconHeight), 0, drawY, 0.4);
            }
            else if (i == idx)
            {
                iconBar.DrawImage(OsuDrawerCommon.GetRankIcon(scoreRank).Resize(iconWidth, iconHeight), 0, drawY);
            }
            else
            {
                var icon = OsuDrawerCommon.GetRankIcon(s);
                icon.Mutate(im => im.Resize(iconWidth, iconHeight).Grayscale(1));

                iconBar.DrawImage(icon, 0, drawY, 0.1);
            }

            drawY -= iconHeight + iconGap;
        }

        return iconBar;
    }

    private static Image GetScoreHeader(Beatmap beatmap, Beatmapset beatmapset)
    {
        var songInfo = new Image<Rgba32>(ImageWidth, 200);

        const int elementGap = 20;

        const int songNameMarginTop = 20;

        var songInfoDrawX = MarginX;
        var songInfoDrawY = songNameMarginTop;

        // song name
        var songNameFont    = _fontExo2.CreateFont(60);
        var songName        = $"{beatmapset.TitleUnicode} by {beatmapset.ArtistUnicode}";
        var songNameMeasure = songName.MeasureWithSpace(songNameFont);

        var statusMark = GetStatusIcon(beatmap.Status);

        while (songNameMeasure.Width > ImageWidth - MarginX * 2 - statusMark.Width - elementGap)
        {
            songName        = songName[..^5] + "...";
            songNameMeasure = songName.MeasureWithSpace(songNameFont);
        }

        songInfo.Clear(BgColor);

        songInfo.DrawImage(statusMark, songInfoDrawX, songInfoDrawY + 10);
        songInfoDrawX += statusMark.Width + elementGap;
        songInfo.DrawText(songName, songNameFont, Color.White, songInfoDrawX, songInfoDrawY);

        // game mode icon
        const int songTypeSize      = 60;
        const int songTypeMarginTop = 20;

        songInfoDrawX = MarginX;
        songInfoDrawY = (int)songNameMeasure.Height + songNameMarginTop + songTypeMarginTop;

        var songTypeIcon = OsuDrawerCommon.GetIcon(beatmap.Mode).ResizeX(songTypeSize);

        songInfo.DrawImage(songTypeIcon, songInfoDrawX, songInfoDrawY);

        // star rating
        const int starRatingPaddingX = 15;

        songInfoDrawX += songTypeIcon.Width + elementGap;

        var starRatingText    = $"★ {beatmap.StarRating:F2}";
        var starRatingFont    = _fontExo2.CreateFont(35, FontStyle.Bold);
        var starRatingMeasure = starRatingText.MeasureWithSpace(starRatingFont);

        var starRating = new Image<Rgba32>((int)starRatingMeasure.Width + starRatingPaddingX * 2, songTypeSize);

        starRating
            .Clear(GetStarRatingColor(beatmap.StarRating))
            .DrawTextCenter(starRatingText, starRatingFont, beatmap.StarRating < 9 ? Color.Black : Color.White)
            .RoundCorners(starRating.Height / 2);

        songInfo.DrawImage(starRating, songInfoDrawX, songInfoDrawY);

        // level name
        songInfoDrawX += starRating.Width + elementGap;

        var levelName    = beatmap.Version;
        var levelFont    = _fontExo2.CreateFont(45);
        var levelMeasure = levelName.MeasureWithSpace(levelFont);

        songInfo.DrawText(levelName, levelFont, Color.White, songInfoDrawX, songInfoDrawY);

        // mapper
        songInfoDrawX += (int)levelMeasure.Width + elementGap;

        var mapperName = $"谱师：{beatmapset.Creator}";
        var mapperFont = _fontExo2.CreateFont(45, FontStyle.Bold);

        songInfo.DrawText(mapperName, mapperFont, Color.ParseHex("#7296a3"), songInfoDrawX, songInfoDrawY);

        return songInfo;
    }

    private static Image GetAccRing(string rank, double acc, int modeInt)
    {
        var fontCollection = new FontCollection();

        var fontVenera = fontCollection.Add(Path.Join(OsuDrawerCommon.ResourcePath, "Venera-700.otf"));
        fontCollection.AddSystemFonts();

        var rankFont = fontVenera.CreateFont(120);

        var accRing = new Image<Rgba32>(400, 400);

        var gradeRingCenter = new Point(accRing.Width / 2, accRing.Height / 2);

        var rankIndex = modeInt switch
        {
            0 => new[] { 1, 0.99, 0.9333, 0.8667, 0.8, 0.6 },
            1 => new[] { 1, 0.99, 0.95, 0.9, 0.8 },
            _ => new[] { 1, 0.99, 0.95, 0.9, 0.8, 0.7 }
        };

        var rankRingColor = new[]
        {
            Color.ParseHex("#be0089"),
            Color.ParseHex("#0096a2"),
            Color.ParseHex("#72c904"),
            Color.ParseHex("#d99d03"),
            Color.ParseHex("#ea7948"),
            Color.ParseHex("#ff5858")
        };

        foreach (var (angle, i) in rankIndex.Select((angle, i) => (angel: angle, i)))
        {
            accRing.Mutate(im => im
                .Fill(rankRingColor[i], ShapeDraw.BuildRing(gradeRingCenter, 145, 10, (int)(360 * angle)))
            );
        }

        var accRingBrush = new LinearGradientBrush(
            new PointF(gradeRingCenter.X, 0), new PointF(gradeRingCenter.X, accRing.Height),
            GradientRepetitionMode.None,
            new ColorStop(0, Color.ParseHex("#66ccfe")), new ColorStop(1, Color.ParseHex("#b3ff67"))
        );

        accRing.Mutate(im => im
            .Fill(Color.Black, ShapeDraw.BuildRing(gradeRingCenter, accRing.Height / 2, 50, 360))
            .Fill(accRingBrush, ShapeDraw.BuildRing(gradeRingCenter, accRing.Height / 2, 50, (int)(360 * acc)))
        );

        var im = new Image<Rgba32>(300, 300);

        rank = rank.Trim('h').Trim('H');
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);
        im.Mutate(i => i.GaussianBlur(15));
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);

        accRing.DrawImageCenter(im);

        return accRing;
    }

    private static Image<Rgba32>? _starRatingColorGradiant;

    private static Color GetStarRatingColor(double starRating)
    {
        if (starRating > 10) starRating = 10;
        if (starRating < 0) starRating  = 0;

        if (_starRatingColorGradiant == null)
        {
            var brush = new LinearGradientBrush(
                new PointF(0, 0), new PointF(2000, 0),
                GradientRepetitionMode.None,
                new ColorStop(0, Color.ParseHex("#4290ff")),
                new ColorStop(0.20f, Color.ParseHex("#4fc0ff")),
                new ColorStop(0.27f, Color.ParseHex("#7cff4f")),
                new ColorStop(0.35f, Color.ParseHex("#f6f05c")),
                new ColorStop(0.50f, Color.ParseHex("#ff4e6f")),
                new ColorStop(0.65f, Color.ParseHex("#c645b8")),
                new ColorStop(0.75f, Color.ParseHex("#6563de")),
                new ColorStop(0.90f, Color.ParseHex("#12106d")),
                new ColorStop(1.00f, Color.ParseHex("#000000"))
            );

            _starRatingColorGradiant = new Image<Rgba32>(2000, 50);
            _starRatingColorGradiant.Mutate(i => i.Fill(brush));
        }

        var x = (int)(starRating / 10.0 * _starRatingColorGradiant.Width);

        if (x == _starRatingColorGradiant.Width) x--;

        return _starRatingColorGradiant[x, _starRatingColorGradiant.Height / 2];
    }

    private static readonly Dictionary<string, Image> ModIconCache = new();

    private static Image GetModIcon(string mod)
    {
        mod = mod.ToUpper();

        lock (ModIconCache)
        {
            if (ModIconCache.ContainsKey(mod)) return ModIconCache[mod].CloneAs<Rgba32>();
        }

        var (iconId, type) = OsuFont.GetModeCharacter(mod);
        var color = OsuFont.GetColorByModeType(type);

        var border = OsuFont.GetCharacter(OsuFont.BorderChar);
        border.Mutate(i =>
        {
            i.SetGraphicsOptions(new GraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
            });
            i.Fill(color.Item1);
        });

        if (iconId == 0)
        {
            var f = _fontExo2.CreateFont(40);
            border.DrawTextCenter(mod, f, color.Item2, withSpace: false);
        }
        else
        {
            var icon = OsuFont.GetCharacter(iconId).ResizeY(26);
            icon.Mutate(i =>
            {
                i.SetGraphicsOptions(new GraphicsOptions
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                });
                i.Fill(color.Item2);
            });

            border.DrawImageCenter(icon, offsetY: -10);

            var font = _fontExo2.CreateFont(15);

            var m = mod.Measure(font);

            var imgText = new Image<Rgba32>((int)(m.Width + 15), (int)(m.Height + 10)).Clear(color.Item2);
            imgText.DrawTextCenter(mod.ToUpper(), font, color.Item1, withSpace: false);

            border.DrawImageCenter(imgText.RoundCorners(imgText.Height / 2 + 1), offsetY: 17);
        }

        lock (ModIconCache)
        {
            ModIconCache[mod] = border;
            return border.CloneAs<Rgba32>();
        }
    }
}