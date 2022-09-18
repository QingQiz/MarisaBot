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

        grade.DrawImageVCenter(GetGradeDetail(score.Score, score.CreatedAt), MarginX + iconBar.Width + ringMarginLeft + ring.Width + gradeCardMarginLeft);

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

        // TODO std taiko catch
        var c4 = GetKeyValuePair("MAX", $"{score.Statistics.CountGeki:N0}", (ImageWidth - staCardGap * 5) / 6);
        var c5 = GetKeyValuePair("300", $"{score.Statistics.Count300:N0}", (ImageWidth - staCardGap * 5) / 6);
        var c6 = GetKeyValuePair("200", $"{score.Statistics.CountKatu:N0}", (ImageWidth - staCardGap * 5) / 6);
        var c7 = GetKeyValuePair("100", $"{score.Statistics.Count100:N0}", (ImageWidth - staCardGap * 5) / 6);
        var c8 = GetKeyValuePair("50", $"{score.Statistics.Count50:N0}", (ImageWidth - staCardGap * 5) / 6);
        var c9 = GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", (ImageWidth - staCardGap * 5) / 6);

        var sta = new Image<Rgba32>(ImageWidth, c1.Height + c4.Height + staCardVGap).Clear(BgColor);

        sta.DrawImage(c1, 0, 0);
        sta.DrawImage(c2, c1.Width + staCardGap, 0);
        sta.DrawImage(c3, (c1.Width + staCardGap) * 2, 0);

        sta.DrawImage(c4, 0, c1.Height + staCardVGap);
        sta.DrawImage(c5, (c4.Width + staCardGap) * 1, c1.Height + staCardVGap);
        sta.DrawImage(c6, (c4.Width + staCardGap) * 2, c1.Height + staCardVGap);
        sta.DrawImage(c7, (c4.Width + staCardGap) * 3, c1.Height + staCardVGap);
        sta.DrawImage(c8, (c4.Width + staCardGap) * 4, c1.Height + staCardVGap);
        sta.DrawImage(c9, (c4.Width + staCardGap) * 5, c1.Height + staCardVGap);
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

    private static Image<Rgba32> GetGradeDetail(long grade, DateTimeOffset time)
    {
        var gradeText = grade.ToString("N0");
        var timeText  = (time + TimeSpan.FromHours(8)).ToString("// yyyy-MM-dd hh:mm:ss");

        var card = new Image<Rgba32>(700, 500);

        var font1 = _fontExo2.CreateFont(160);
        var font3 = _fontExo2.CreateFont(40);

        // TODO draw mods
        card.DrawText(gradeText, font1, Color.White, 0, 100);
        card.DrawText(timeText, font3, Color.White, 10, 300);

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
        var songNameFont = _fontExo2.CreateFont(60);
        // TODO 这里应该画图
        var songName        = $"{beatmapset.TitleUnicode} by {beatmapset.ArtistUnicode}";
        var songNameMeasure = songName.MeasureWithSpace(songNameFont);

        while (songNameMeasure.Width > ImageWidth - MarginX * 2)
        {
            songName        = songName[..^5] + "...";
            songNameMeasure = songName.MeasureWithSpace(songNameFont);
        }

        songName = songName + $"({beatmap.Status})";

        songInfo.Clear(BgColor);
        songInfo.DrawText(songName, songNameFont, Color.White, songInfoDrawX, songInfoDrawY);

        // game mode icon
        const int songTypeSize      = 60;
        const int songTypeMarginTop = 20;

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
}