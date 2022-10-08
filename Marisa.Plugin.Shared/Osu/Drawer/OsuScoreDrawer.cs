using System.Numerics;
using Flurl.Http;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuScoreDrawer
{
    private static async Task<Image> GetCover(this OsuScore score)
    {
        var coverList = new[]
        {
            score.Beatmapset.Covers.Cover2X,
            score.Beatmapset.Covers.Card2X,
            score.Beatmapset.Covers.Cover,
            score.Beatmapset.Covers.Card,
            score.Beatmapset.Covers.Slimcover2X,
            score.Beatmapset.Covers.Slimcover,
        };

        foreach (var uri in coverList)
        {
            try
            {
                return await OsuDrawerCommon.GetCacheOrDownload(uri, "jpg");
            }
            catch (FlurlHttpException e) when (e.StatusCode == 404)
            {
            }
        }

        return new Image<Rgba32>(ImageWidth, 500).Clear(Color.Black);
    }

    private const int ImageWidth = 2000;
    private const int MarginX = 100;

    private static FontFamily _fontExo2 = SystemFonts.Get("Exo 2");
    private static FontFamily _fontYaHei = SystemFonts.Get("Microsoft YaHei");
    private static FontFamily _fontIcon = SystemFonts.Get("Segoe UI Symbol");
    private static readonly Color BgColor = Color.FromRgb(46, 53, 56);

    public static Image GetMiniCards(this List<(OsuScore, int)> score)
    {
        const int gap    = 5;
        const int margin = 20;

        var ims = score.Select(x  => GetMiniCard(x.Item1)).ToList();

        var image = new Image<Rgba32>(margin * 2 + ims[0].Width, margin * 2 + ims.Count * ims[0].Height + (ims.Count - 1) * gap).Clear(Color.ParseHex("#382e32"));

        var drawY = margin;

        var font = _fontExo2.CreateFont(16);

        for (var i = 0; i < ims.Count; i++)
        {
            image.DrawImage(ims[i], margin, drawY);
            image.DrawText($"#{score[i].Item2 + 1}", font, Color.White, margin + 10, drawY + 5);
            drawY += ims[i].Height + gap;
        }

        return image;
    }

    public static Image GetMiniCard(this OsuScore score)
    {
        const int width   = 2000;
        const int height  = 100;
        const int marginX = 40;
        const int marginY = 10;
        const int gap     = 20;

        var im = new Image<Rgba32>(width, height).Clear(Color.FromRgb(84, 69, 76));

        var rankIcon = OsuDrawerCommon.GetRankIcon(score.Rank).ResizeY(50);
        {
            im.DrawImageVCenter(rankIcon, marginX);
        }

        {
            var font = _fontExo2.CreateFont(28);

            var opt = ImageDraw.GetTextOptions(font);

            opt.VerticalAlignment = VerticalAlignment.Bottom;
            opt.Origin            = new Vector2(marginX + rankIcon.Width + gap, height - marginY);

            im.DrawText(opt, score.Beatmap.Version, Color.FromRgb(238, 170, 0));

            var ago = score.CreatedAt.TimeAgo();

            opt.Origin = new Vector2(opt.Origin.X + score.Beatmap.Version.MeasureWithSpace(font).Width + gap, opt.Origin.Y + 5);

            im.DrawText(opt, ago, Color.FromRgb(163, 143, 152));
        }

        var rec = new Image<Rgba32>(200, height);
        {
            var pb = new PathBuilder();

            pb.AddLines(new PointF(0, 0), new PointF(rec.Width, 0), new PointF(rec.Width, rec.Height), new PointF(0, rec.Height),
                new PointF(20, (float)(rec.Height / 2.0))
            );

            rec.Mutate(i => i.Fill(Color.FromRgb(70, 57, 63), pb.Build()));

            var text1 = $"{score.Pp:N0}";
            var text2 = "pp";
            var font1 = _fontExo2.CreateFont(40, FontStyle.Bold);
            var font2 = _fontExo2.CreateFont(30);

            var w1 = text1.MeasureWithSpace(font1).Width;
            var w2 = text2.MeasureWithSpace(font2).Width;

            var x1 = 20 + (rec.Width - 20 - w1 - w2) / 2;
            var x2 = x1 + w1;

            rec.DrawTextVCenter(text1, font1, Color.FromRgb(255, 102, 171), (int)x1);
            rec.DrawTextVCenter(text2, font2, Color.FromRgb(209, 148, 175), (int)x2);

            im.DrawImage(rec, im.Width - rec.Width, 0);
        }

        {
            var truePp = $"{score.Weight?.Pp ?? 0:N0}pp";

            var font = _fontExo2.CreateFont(40, FontStyle.Bold);

            var opt = ImageDraw.GetTextOptions(font);

            opt.HorizontalAlignment = HorizontalAlignment.Right;
            opt.Origin              = new Vector2(width - rec.Width - gap, (height - truePp.MeasureWithSpace(font).Height) / 2);

            im.DrawText(opt, truePp, Color.White);
        }

        {
            var acc    = $"{score.Accuracy * 100:F2}%";
            var weight = $"权重：{score.Weight?.Percentage ?? 0:F0}%";

            var font1 = _fontExo2.CreateFont(33, FontStyle.Bold);
            var font2 = _fontYaHei.CreateFont(28);

            var opt = ImageDraw.GetTextOptions(font1);

            opt.Origin = new Vector2(1460, marginY);

            im.DrawText(opt, acc, Color.ParseHex("#FFCC22"));

            opt.Font              = font2;
            opt.Origin            = opt.Origin with { Y = height - marginY };
            opt.VerticalAlignment = VerticalAlignment.Bottom;

            im.DrawText(opt, weight, Color.White);
        }

        var modIconDrawX = 1430;
        {
            const int modIconWidth = 80;
            const int iconGap      = 10;

            var icons = score.Mods.Select(GetModIconWithoutText);

            foreach (var i in icons)
            {
                var draw = i.ResizeX(modIconWidth);
                modIconDrawX -= draw.Width;

                im.DrawImageVCenter(draw, modIconDrawX);
                modIconDrawX -= iconGap;
            }
        }

        {
            var font = _fontExo2.CreateFont(35);

            var text = score.Beatmapset.TitleUnicode + " by " + score.Beatmapset.ArtistUnicode;

            while (text.MeasureWithSpace(font).Width + marginX + rankIcon.Width + gap > modIconDrawX)
            {
                text = text[..^4] + "...";
            }

            im.DrawText(text, font, Color.White, marginX + rankIcon.Width + gap, marginY);
        }


        return im.RoundCorners(15);
    }

    public static async Task<Image> GetImage(this OsuScore score, OsuUserInfo info)
    {
        var scoreHeader = GetScoreHeader(score);

        // 封面
        var cover = (await score.GetCover()).Fit(ImageWidth, 650);
        var grade = new Image<Rgba32>(ImageWidth, cover.Height);
        grade.DrawImage(cover, 0, 0).Clear(Color.FromRgba(0, 0, 0, 175));

        // 成绩的 rank 轴
        var iconBar = IconBar(score.Rank);
        grade.DrawImageVCenter(iconBar, MarginX);

        // 成绩环
        const int ringMarginLeft = 50;

        var ring = GetAccRing(score.Rank, score.Accuracy, score.ModeInt).Resize(1.2);
        grade.DrawImageVCenter(ring, MarginX + iconBar.Width + ringMarginLeft);

        // 谱面详情
        var beatmapDetail = GetBeatmapDetail(score.Beatmap);
        grade.DrawImageVCenter(beatmapDetail, ImageWidth - MarginX - beatmapDetail.Width);

        // 成绩数值
        // BUG std 成绩太长了，显示会出问题
        const int gradeCardMarginLeft = 50;

        var maxWidth = ImageWidth - MarginX - iconBar.Width - ringMarginLeft - ring.Width - gradeCardMarginLeft - MarginX - beatmapDetail.Width;

        grade.DrawImageVCenter(GetGradeAndMods(maxWidth, score.Score, score.Mods, score.CreatedAt),
            MarginX + iconBar.Width + ringMarginLeft + ring.Width + gradeCardMarginLeft);

        // 玩家卡片
        var userCard = (await info.GetMiniCard()).ResizeX((int)((ImageWidth - MarginX * 2) * 0.4));

        var sta = GetScoreSta(score).ResizeX(ImageWidth - MarginX * 2 - userCard.Width - MarginX);

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

    private static Image GetScoreSta(OsuScore score)
    {
        const int staCardGap  = 2;
        const int staCardVGap = 60;

        var pp = score.GetPerformance().ToString("F2");

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

    private static Image<Rgba32> GetGradeAndMods(int maxWidth, long grade, string[] mods, DateTimeOffset time)
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

    private static Image GetScoreHeader(OsuScore score)
    {
        var beatmap    = score.Beatmap;
        var beatmapset = score.Beatmapset;

        var songInfo = new Image<Rgba32>(ImageWidth, 200).Clear(BgColor);

        const int elementGap = 20;

        const int songNameMarginTop = 20;

        var songInfoDrawX = MarginX;
        var songInfoDrawY = songNameMarginTop;

        // 歌曲状态，ranked、loved 等
        var statusMark = GetStatusIcon(beatmap.Status);

        songInfo.DrawImage(statusMark, songInfoDrawX, songInfoDrawY + 10);

        // song name
        var songNameFont    = _fontExo2.CreateFont(60);
        var songName        = $"{beatmapset.TitleUnicode} by {beatmapset.ArtistUnicode}";
        var songNameMeasure = songName.MeasureWithSpace(songNameFont);

        songInfoDrawX += statusMark.Width + elementGap;

        // 调整歌名，让他不要超过图片宽度
        while (songNameMeasure.Width + songInfoDrawX + elementGap > ImageWidth)
        {
            songName        = songName[..^4] + "...";
            songNameMeasure = songName.MeasureWithSpace(songNameFont);
        }

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

        var starRating        = score.GetStarRating();
        var starRatingText    = $"★ {starRating:F2}";
        var starRatingFont    = _fontExo2.CreateFont(35, FontStyle.Bold);
        var starRatingMeasure = starRatingText.MeasureWithSpace(starRatingFont);

        var starRatingImg = new Image<Rgba32>((int)starRatingMeasure.Width + starRatingPaddingX * 2, songTypeSize);

        starRatingImg
            .Clear(GetStarRatingColor(starRating))
            .DrawTextCenter(starRatingText, starRatingFont, starRating < 9 ? Color.Black : Color.White)
            .RoundCorners(starRatingImg.Height / 2);

        songInfo.DrawImage(starRatingImg, songInfoDrawX, songInfoDrawY);

        // level name
        var mapperName = $"谱师：{beatmapset.Creator}";
        var mapperFont = _fontExo2.CreateFont(45, FontStyle.Bold);

        var mapperWidth = mapperName.MeasureWithSpace(mapperFont).Width;

        songInfoDrawX += starRatingImg.Width + elementGap;

        var levelName = beatmap.Version;
        var levelFont = _fontExo2.CreateFont(45);

        // 调整 levelName 字符串，让他不要超出宽度
        while (levelName.MeasureWithSpace(levelFont).Width + mapperWidth + songInfoDrawX + elementGap > ImageWidth)
        {
            levelName = levelName[..^4] + "...";
        }

        var levelMeasure = levelName.MeasureWithSpace(levelFont);

        songInfo.DrawText(levelName, levelFont, Color.White, songInfoDrawX, songInfoDrawY);

        // mapper
        songInfoDrawX += (int)levelMeasure.Width + elementGap;

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
    private static readonly Dictionary<string, Image> ModIconCacheWithoutText = new();

    private static Image GetModIconWithoutText(string mod)
    {
        mod = mod.ToUpper();

        lock (ModIconCacheWithoutText)
        {
            if (ModIconCacheWithoutText.ContainsKey(mod)) return ModIconCacheWithoutText[mod].CloneAs<Rgba32>();
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
            var icon = OsuFont.GetCharacter(iconId).ResizeY(border.Height - 10);
            icon.Mutate(i =>
            {
                i.SetGraphicsOptions(new GraphicsOptions
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                });
                i.Fill(color.Item2);
            });

            border.DrawImageCenter(icon);
        }

        lock (ModIconCacheWithoutText)
        {
            ModIconCacheWithoutText[mod] = border;
            return border.CloneAs<Rgba32>();
        }
    }

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

    private static Image GetBeatmapDetail(Beatmap beatmap)
    {
        return new CacheableImage(Path.Join(OsuDrawerCommon.TempPath, "BeatmapDetail-") + beatmap.Checksum + ".png", () =>
        {
            const int imageWidth = 500, margin = 20, padding = 10;

            // same to BgColor, but with transparent
            var bgColor     = Color.FromRgba(46, 53, 56, (byte)(0.2 * 255));
            var colorYellow = Color.ParseHex("#ffdd55");

            Image BeatmapCounter()
            {
                const int iconWidth = 40;

                var length  = TimeSpan.FromSeconds(beatmap.HitLength).ToString("m':'s");
                var bpm     = beatmap.Bpm.ToString();
                var circles = beatmap.CountCircles.ToString("N0");
                var sliders = beatmap.CountSliders.ToString("N0");

                var font = _fontExo2.CreateFont(20, FontStyle.Bold);

                var img = new Image<Rgba32>(imageWidth, iconWidth + margin * 2).Clear(bgColor);

                var step = (int)((imageWidth - margin * 2 - sliders.MeasureWithSpace(font).Width - padding - iconWidth) / 3);

                var x = margin;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("total_length").ResizeX(iconWidth), x);
                img.DrawTextVCenter(length, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("bpm").ResizeX(iconWidth), x);
                img.DrawTextVCenter(bpm, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("count_circles").ResizeX(iconWidth), x);
                img.DrawTextVCenter(circles, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("count_sliders").ResizeX(iconWidth), x);
                img.DrawTextVCenter(sliders, font, colorYellow, x + iconWidth + padding);

                return img;
            }

            Image BeatmapConfig()
            {
                const int barHeight      = 40;
                const int barHeightInner = 10;

                List<(string, double)> kv = new();

                switch (beatmap.ModeInt)
                {
                    case 3:
                    {
                        kv.AddRange(new[]
                        {
                            ("键位数量", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 0:
                    {
                        kv.AddRange(new[]
                        {
                            ("圆圈大小", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("缩圈速度", beatmap.Ar),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 1:
                    {
                        kv.AddRange(new[]
                        {
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 2:
                    {
                        kv.AddRange(new[]
                        {
                            ("圆圈大小", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("缩圈速度", beatmap.Ar),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                }

                var img = new Image<Rgba32>(imageWidth, barHeight * kv.Count + margin * 2 + padding * (kv.Count - 1)).Clear(bgColor);

                var font = _fontYaHei.CreateFont(20);

                var (_, _, kWidth, kHeight) = kv[0].Item1.MeasureWithSpace(font);

                var vWidth   = kv.Max(x => x.Item2.ToString("0.##").MeasureWithSpace(font).Width);
                var barWidth = (int)(imageWidth - margin * 2 - padding * 2 - kWidth - vWidth);

                for (var i = 0; i < kv.Count; i++)
                {
                    var color = i == kv.Count - 1 ? colorYellow : Color.White;

                    var v  = kv[i].Item2.ToString("0.##");
                    var vM = v.MeasureWithSpace(font).Width;

                    var x2 = (int)(margin + kWidth + padding);
                    var x3 = (int)(imageWidth + kWidth + padding + barWidth + padding - vM) / 2;
                    var y1 = (int)(margin + i * (barHeight + padding) + (barHeight - kHeight) / 2);
                    var y2 = margin + i * (barHeight + padding) + (barHeight - barHeightInner) / 2;

                    img.DrawText(kv[i].Item1, font, Color.White, margin, y1);

                    var rect1 = new Rectangle(x2, y2, barWidth, barHeightInner);
                    var rect2 = new Rectangle(x2, y2, (int)(barWidth * (Math.Min(kv[i].Item2, 10.0) / 10)), barHeightInner);

                    img.Mutate(x => x.Fill(Color.Black, rect1).Fill(color, rect2));
                    img.DrawText(v, font, Color.White, x3, y1);
                }

                return img;
            }

            var im1 = BeatmapCounter();
            var im2 = BeatmapConfig();

            var im = new Image<Rgba32>(imageWidth, im1.Height + 3 + im2.Height);
            im.DrawImage(im1, 0, 0);
            im.DrawImage(im2, 0, im1.Height + 3);

            return im;
        }).Value;
    }
}