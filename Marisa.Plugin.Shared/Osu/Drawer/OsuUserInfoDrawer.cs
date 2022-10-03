using System.Diagnostics.CodeAnalysis;
using Marisa.Plugin.Shared.Osu.Entity.PPlus;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

namespace Marisa.Plugin.Shared.Osu.Drawer;

[SuppressMessage("ReSharper", "PossibleLossOfFraction")]
public static class OsuUserInfoDrawer
{
    private static async Task<PPlus> GetPPlus(long uid)
    {
        var filename = $"pp+cache-{uid}-{DateTime.Now:yyyy-MM}.json";
        var path     = Path.Join(OsuDrawerCommon.TempPath, filename);

        if (File.Exists(path))
        {
            return PPlus.FromJson(await File.ReadAllTextAsync(path));
        }

        var pPlus = await OsuApi.GetPPlusJsonById(uid);

        await File.WriteAllTextAsync(path, pPlus);

        return PPlus.FromJson(pPlus);
    }

    private static Image GetMode(string mode)
    {
        return Image.Load(Path.Join(OsuDrawerCommon.ResourcePath, $"mode-{mode}.png")).CloneAs<Rgba32>();
    }

    private const int ImageWidth = 2000;
    private const int MarginX = 50;

    private static readonly FontFamily FontExo2 = SystemFonts.Get("Exo 2");
    private static readonly FontFamily FontYaHei = SystemFonts.Get("Microsoft YaHei");
    private static readonly FontFamily FontIcon = SystemFonts.Get("Segoe UI Symbol");

    private static readonly Color FontColor = Color.FromRgb(240, 219, 228);

    public static async Task<Image> GetImage(this OsuUserInfo info)
    {
        // header
        var header = GetHeader(info.RankHistory!.Mode);

        // banner
        var nameBanner = await GetNameBanner(info);

        const int detailWidth = ImageWidth / 2 - 150;

        // rank
        var rank = GetRankAndPerformance(detailWidth, info.Statistics);

        // rank history
        var chart = GetRankHistoryChart(detailWidth, info.RankHistory!.Data);

        // counter 奖章、pp、游戏时间、ss个数等
        var counter = GetGradeCounter(detailWidth, info.UserAchievements.Length, info.Statistics);

        // 汇总信息
        var summary = GetUserSummary(info.Statistics, rank.Height + chart.Height + counter.Height + 40);

        // PP+
        var pPlusChart = await GetPPlusChart(info.RankHistory!.Mode, info.Id);

        // 把上面几个小卡片拼接起来
        var userDetail = CombineUserDetailCard(summary, rank, chart, counter, pPlusChart);

        // 狗牌
        if (info.Badges.Any())
        {
            var badges = await GetAllBadges(info.Badges);

            userDetail = badges.Aggregate(userDetail, (current, badge) =>
                new Image<Rgba32>(current.Width, current.Height + badge.Height)
                    .DrawImage(current, 0, badge.Height).DrawImage(badge, 0, 0).CloneAs<Rgba32>()
            );
        }

        // 把它们拼起来
        var bg = new Image<Rgba32>(ImageWidth, header.Height + nameBanner.Height + userDetail.Height);

        var pen2 = new Pen(Color.FromRgb(37, 30, 34), 1);
        bg.Mutate(i => i
            .DrawImage(header, 0, 0)
            .DrawImage(nameBanner, 0, header.Height)
            .DrawLines(pen2, new PointF(0, header.Height + nameBanner.Height), new PointF(ImageWidth, header.Height + nameBanner.Height))
            .DrawImage(userDetail, 0, header.Height + nameBanner.Height)
        );

        return bg.RoundCorners(40);
    }

    public static async Task<Image> GetMiniCard(this OsuUserInfo info)
    {
        const int flagHeight = 70;
        const int gap        = 40;
        const int margin     = 40;

        var image = new Image<Rgba32>(1200, 400).Clear(Color.Black);

        var avatar = (await OsuDrawerCommon.GetAvatar(info.AvatarUrl)).Resize(200, 200).RoundCorners(40);
        var banner = await OsuDrawerCommon.GetCacheOrDownload(info.Cover.Url);

        banner.Fit(image.Width, image.Height);

        image.DrawImage(banner, 0, 0, 0.6);
        image.DrawImage(avatar, margin, margin);

        var regionIcon = OsuDrawerCommon.GetIcon(info.Region.Code.ToLower()).ResizeY(flagHeight).RoundCorners(12);

        image.DrawImage(regionIcon, margin + avatar.Width + gap, margin);

        if (info.IsSupporter)
        {
            var fontSupporter = new Font(FontIcon, 52, FontStyle.Regular);

            var supportImg = new Image<Rgba32>(flagHeight, flagHeight).Clear(Color.FromRgb(255, 102, 171));

            supportImg.DrawTextCenter("♥", fontSupporter, Color.White);

            image.DrawImage(supportImg.RoundCorners(flagHeight / 2), margin + avatar.Width + gap + regionIcon.Width + gap, margin);
        }

        var fontName = new Font(FontExo2, 80);
        image.DrawText(info.Username, fontName, Color.White, margin + avatar.Width + gap, margin + flagHeight + gap / 2);

        const int ringRadius = 40;

        var ringCenter = new PointF(margin + avatar.Width / 2, margin + avatar.Height + margin + ringRadius);
        var ring       = ShapeDraw.BuildRing(ringCenter, ringRadius, 15, 360);

        image.Mutate(i => i.Fill(info.IsOnline ? Color.ParseHex("#b3d944") : Color.Black, ring));

        var font = new Font(FontYaHei, 40);

        if (info.IsOnline)
        {
            var option = ImageDraw.GetTextOptions(font, new PointF(margin + avatar.Width + gap, ringCenter.Y));
            option.VerticalAlignment = VerticalAlignment.Center;

            image.Mutate(i => i.DrawText(option, "在线", Color.White));
        }
        else
        {
            var option = ImageDraw.GetTextOptions(font, new PointF(margin + avatar.Width + gap, ringCenter.Y));
            option.VerticalAlignment = VerticalAlignment.Top;

            image.Mutate(i => i.DrawText(option, "离线", Color.White));

            option.VerticalAlignment = VerticalAlignment.Bottom;

            var lastActivate = info.LastVisit ?? DateTime.MaxValue;

            var text = "最后活跃：";

            if ((DateTime.Now - lastActivate).TotalDays < 1)
            {
                text += $"{(DateTime.Now - lastActivate).TotalHours:N0} 小时前";
            }
            else
            {
                text += $"{(DateTime.Now - lastActivate).TotalDays:N0} 天前";
            }

            image.Mutate(i => i.DrawText(option, text, Color.White));
        }

        return image.RoundCorners(40);
    }

    private static Image<Rgba32> CombineUserDetailCard(
        Image summary, Image rank, Image chart, Image counter, Image pPlusChart)
    {
        var userDetail = new Image<Rgba32>(ImageWidth, summary.Height + 60);

        var gap = (userDetail.Height - (rank.Height + chart.Height + counter.Height) - 20) / 2;

        userDetail.Clear(Color.FromRgb(42, 34, 38));
        pPlusChart.ResizeY(userDetail.Height - 80);

        var pen1 = new Pen(Color.FromRgb(28, 23, 25), 2);

        const int lineGap      = 30;
        const int marginBorder = 10;

        var xSummary = userDetail.Width - MarginX - summary.Width;
        var xLine1   = MarginX + Math.Max(rank.Width, Math.Max(chart.Width, counter.Width)) + lineGap;
        var xLine2   = xSummary - lineGap;
        var xPPlus   = ((xLine2 - lineGap - (xLine1 + lineGap)) - pPlusChart.Width) / 2 + xLine1 + lineGap;

        userDetail.Mutate(i => i
            .DrawImage(rank, MarginX, marginBorder)
            .DrawImage(chart, MarginX, marginBorder + rank.Height + gap)
            .DrawImage(counter, MarginX, marginBorder + rank.Height + chart.Height + gap + gap)
            .DrawLines(pen1, new PointF(xLine1, marginBorder), new PointF(xLine1, userDetail.Height - marginBorder))
            .DrawLines(pen1, new PointF(xLine2, marginBorder), new PointF(xLine2, userDetail.Height - marginBorder))
            .DrawImageVCenter(pPlusChart, xPPlus)
            .DrawImageVCenter(summary, userDetail.Width - summary.Width - MarginX)
        );
        return userDetail;
    }

    private static Image<Rgba32> GetUserSummary(Statistics st, int height)
    {
        var font1 = new Font(FontYaHei, 32, FontStyle.Regular);
        var font2 = new Font(FontExo2, 36, FontStyle.Bold);

        var textHeader = new[]
        {
            "Ranked 谱面总分", "准确率", "游戏次数", "回放被观看次数", "总命中次数", "最大连击", "总分"
        };

        var value = new[]
        {
            st.RankedScore.ToString("N0"),
            st.HitAccuracy.ToString("F2") + "%",
            st.PlayCount.ToString("N0"),
            st.ReplaysWatchedByOthers.ToString("N0"),
            st.TotalHits.ToString("N0"),
            st.MaximumCombo.ToString("N0"),
            st.TotalScore.ToString("N0"),
        };

        const int xGap = 20;

        var summaryWidth = textHeader.Zip(value).Max(tuple => tuple.First.MeasureWithSpace(font1).Width + tuple.Second.MeasureWithSpace(font2).Width);
        var summary      = new Image<Rgba32>((int)summaryWidth + xGap, height);

        var textHeight = (float)summary.Height / textHeader.Length;
        var lineSpace  = (int)(textHeight - textHeader[0].MeasureWithSpace(font1).Height) / 2;

        for (var i = 0; i < textHeader.Length; i++)
        {
            summary.DrawText(textHeader[i], font1, Color.White, 0, lineSpace + textHeight * i);
        }

        for (var i = 0; i < value.Length; i++)
        {
            var option = ImageDraw.GetTextOptions(font2, new PointF(summary.Width, lineSpace + textHeight * i));

            option.HorizontalAlignment = HorizontalAlignment.Right;

            var text = value[i];
            summary.Mutate(im => im.DrawText(option, text, FontColor));
        }

        return summary;
    }

    private static Image<Rgba32> GetGradeCounter(int detailWidth, int achievementCount, Statistics statistics)
    {
        var counter = new Image<Rgba32>(detailWidth, 74);

        var font1 = new Font(FontYaHei, 24, FontStyle.Regular);
        var font2 = new Font(FontExo2, 32, FontStyle.Bold);
        var font3 = new Font(FontExo2, 24, FontStyle.Bold);

        const string text1 = "奖章";
        const string text3 = "游戏时间";

        // 奖章
        var text1M = text1.MeasureWithSpace(font1);
        counter.DrawText(text1, font1, Color.White, 0, 5);
        counter.DrawText($"{achievementCount:N0}", font2, FontColor, 0, text1M.Height);

        // 游戏时间
        var text3X = Math.Max(text1M.Width, achievementCount.ToString("N0").MeasureWithSpace(font2).Width) + 30;
        counter.DrawText(text3, font1, Color.White, text3X, 5);
        var t = TimeSpan.FromSeconds(statistics.PlayTime);
        counter.DrawText($"{t.Days:N0}d {t.Hours:N0}h {t.Minutes:N0}m", font2, FontColor, text3X, text1M.Height);

        // ss个数、s个数等
        const int iconHeight = 44;
        const int iconWidth  = 88;

        var rankX = counter.Width - iconWidth;

        foreach (var rk in new[] { "a", "s", "sh", "ss", "ssh" })
        {
            var s = statistics.GradeCounts[rk].ToString("N0");
            counter.DrawImage(OsuDrawerCommon.GetRankIcon(rk).ResizeY(iconHeight), rankX, 0);
            var w = s.Measure(font1).Width;
            counter.DrawText(s, font3, Color.White, rankX + (iconWidth - w) / 2, iconHeight);

            rankX -= (int)Math.Max(w, iconWidth) + 5;
        }

        return counter;
    }

    private static Image<Rgba32> GetRankAndPerformance(int detailWidth, Statistics statistics)
    {
        var rank = new Image<Rgba32>(detailWidth, 100);

        const string rankText1 = "全球排名";
        const string rankText2 = "国内/区内排名";

        var font1 = new Font(FontYaHei, 24, FontStyle.Regular);
        var font2 = new Font(FontExo2, 60, FontStyle.Bold);

        var text1H = rankText1.MeasureWithSpace(font1).Height - 8;

        var x = 0;

        var rank1  = $"#{statistics.GlobalRank:N0}";
        var text1W = Math.Max(rankText1.MeasureWithSpace(font1).Width, rank1.MeasureWithSpace(font2).Width);
        rank.DrawText(rankText1, font1, Color.White, x, 0);
        rank.DrawText(rank1, font2, FontColor, x, text1H);

        x += (int)text1W + MarginX;

        var rank2  = $"#{statistics.RegionRank:N0}";
        var text2W = Math.Max(rankText2.MeasureWithSpace(font1).Width, rank2.MeasureWithSpace(font2).Width);
        rank.DrawText(rankText2, font1, Color.White, x, 0);
        rank.DrawText(rank2, font2, FontColor, x, text1H);

        x += (int)text2W + MarginX;

        var ppText = "PP";

        if (statistics.Variants?.Any() ?? false)
        {
            var vFiltered = statistics.Variants.Where(v => v.Pp > 100).ToList();
            if (vFiltered.Count > 1)
            {
                ppText = $"{ppText} ({string.Join(", ", statistics.Variants.Select(v => $"{v.Name}: {v.Pp}"))})";
            }
        }

        var pp = $"{statistics.Pp:F2}";
        rank.DrawText(ppText, font1, Color.White, x, 0);
        rank.DrawText(pp, font2, FontColor, x, text1H);
        return rank;
    }

    private static Image<Rgba32> GetRankHistoryChart(int detailWidth, IEnumerable<long> historyData)
    {
        var chart = new Image<Rgba32>(detailWidth, 120);

        var history = historyData.Where(r => r > 0).ToArray();

        if (history.Length < 2)
        {
            history = history.Length == 0
                ? history.Append(0).Append(0).ToArray()
                : history.Append(history[0]).ToArray();
        }

        var min = history.Min();
        var max = history.Max();

        var points         = new List<PointF>();
        var rankHistoryPen = new Pen(Color.FromRgb(255, 204, 34), 4);

        for (var i = 0; i < history.Length; i++)
        {
            var xNew = i * (float)chart.Width / (history.Length - 1);
            var yNew = (float)(history[i] - min) / (max - min) * (chart.Height - 8) + 4;

            points.Add(new PointF(xNew, yNew));
        }

        chart.Mutate(i => i
            .DrawLines(rankHistoryPen, max != min
                ? points.ToArray()
                : new[] { new PointF(0, chart.Height / 2), new PointF(chart.Width, chart.Height / 2) })
        );
        return chart;
    }

    private static async Task<Image<Rgba32>> GetNameBanner(OsuUserInfo info)
    {
        const int bannerMaxHeight = 600;

        var banner = (await OsuDrawerCommon.GetCacheOrDownload(info.Cover.Url)).ResizeX(ImageWidth);

        if (banner.Height > bannerMaxHeight)
        {
            banner = banner.Crop(0, (banner.Height - bannerMaxHeight) / 2, ImageWidth, bannerMaxHeight);
        }

        var nameBanner = new Image<Rgba32>(ImageWidth, 170 + banner.Height);

        nameBanner.Clear(Color.FromRgb(56, 46, 50));

        // banner
        nameBanner.DrawImage(banner, 0, 0);

        // avatar
        var avatar = (await OsuDrawerCommon.GetAvatar(info.AvatarUrl)).Resize(240, 240).RoundCorners(80);
        nameBanner.DrawImage(avatar, MarginX, nameBanner.Height - 20 - avatar.Height);

        var nameCard = new Image<Rgba32>(1000, 140);

        var font = new Font(FontExo2, 48, FontStyle.Bold);

        // username
        var (_, _, nameWidth, nameHeight) = info.Username.MeasureWithSpace(font);
        nameCard.DrawText(info.Username, font, Color.White, 0, 0);

        // supporter
        if (info.IsSupporter)
        {
            var fontSupporter = new Font(FontIcon, 46, FontStyle.Regular);
            var supporterChar = new string('♥', info.SupportLevel);

            var supportColor = Color.FromRgb(255, 102, 171);
            nameCard.DrawText(supporterChar, fontSupporter, supportColor, nameWidth + 10, 0);
        }

        // region flag
        var regionIcon = OsuDrawerCommon.GetIcon(info.Region.Code.ToLower())
            .ResizeY(50)
            .RoundCorners(12);
        nameCard.DrawImage(regionIcon, 2, (int)(nameHeight + 12));

        // region name
        font = new Font(FontExo2, 32, FontStyle.Bold);
        var regionName   = info.Region.Name + (string.IsNullOrWhiteSpace(info.Title) ? "" : $" // {info.Title}");
        var regionHeight = regionName.MeasureWithSpace(font).Height;
        nameCard.DrawText(regionName, font, Color.White, regionIcon.Width + 10, (nameCard.Height - nameHeight - regionHeight) / 2 + nameHeight);

        var nameCardX = MarginX + avatar.Width + 20;
        var nameCardY = (nameBanner.Height - banner.Height - nameCard.Height) / 2 + banner.Height;
        nameBanner.DrawImage(nameCard, nameCardX, nameCardY);

        // level
        var levelIcon  = OsuDrawerCommon.GetIcon("level").ResizeY(100);
        var levelIconX = nameBanner.Width - MarginX - levelIcon.Width;
        var levelIconY = (nameBanner.Height - banner.Height - levelIcon.Height) / 2 + banner.Height;
        // 等级的框
        nameBanner.DrawImage(levelIcon, levelIconX, levelIconY);

        var levelFont   = new Font(FontExo2, 40, FontStyle.Bold);
        var levelString = info.Statistics.Level.Current.ToString("N0");
        var (_, _, levelStringW, levelStringH) = levelString.MeasureWithSpace(levelFont);
        var levelStringX = levelIconX + (levelIcon.Width - levelStringW) / 2;
        var levelStringY = levelIconY + (levelIcon.Height - levelStringH) / 2;
        // 等级的值
        nameBanner.DrawText(levelString, levelFont, Color.White, levelStringX, levelStringY);

        var levelBar = new Image<Rgba32>(400, 12);
        levelBar.Mutate(i =>
        {
            i.Fill(Color.FromRgb(28, 23, 25));

            if (info.Statistics.Level.Progress != 0)
            {
                var p = new Image<Rgba32>((int)(levelBar.Width * (info.Statistics.Level.Progress / 100.0)), levelBar.Height)
                    .Clear(Color.FromRgb(255, 102, 171))
                    .RoundCorners(levelBar.Height / 2);

                i.DrawImage(p, 0, 0);
            }

            i.RoundCorners(levelBar.Height / 2);
        });

        var levelBarX = levelIconX - 15 - levelBar.Width;
        var levelBarY = levelIconY + (levelIcon.Height - levelBar.Height) / 2;
        // 等级的进度条
        nameBanner.DrawImage(levelBar, levelBarX, levelBarY);

        var progressF = new Font(FontExo2, 24, FontStyle.Regular);
        var progressS = info.Statistics.Level.Progress.ToString("N0") + '%';
        var progressM = progressS.MeasureWithSpace(progressF);
        var progressX = levelBarX + levelBar.Width - progressM.Width;
        var progressY = levelBarY + levelBar.Height + 5;
        nameBanner.DrawText(progressS, progressF, Color.White, progressX, progressY);
        return nameBanner;
    }

    private static Image<Rgba32> GetHeader(string gameMode)
    {
        var header = new Image<Rgba32>(ImageWidth, 110);

        // 头
        header.Clear(Color.FromRgb(61, 41, 50));

        // 标题
        var font = new Font(FontYaHei, 40, FontStyle.Regular);

        var userIcon = OsuDrawerCommon.GetIcon("user").ResizeX(80);
        header.DrawImageVCenter(userIcon, MarginX);

        const string headTitle = "玩家信息";

        var titleX = MarginX + userIcon.Width + 10;
        header.DrawTextVCenter(headTitle, font, Color.White, titleX);

        // 游玩模式
        var mode = GetMode(gameMode).ResizeY(60);
        header.DrawImageVCenter(mode, header.Width - MarginX - mode.Width);
        return header;
    }

    private static async Task<List<Image>> GetAllBadges(IEnumerable<Badge> badgeInfo)
    {
        var badges = new List<Image>
        {
            new Image<Rgba32>(ImageWidth, 120).Clear(Color.FromRgb(42, 34, 38))
        };

        var       bX      = MarginX;
        const int bMargin = 20;

        foreach (var b in badgeInfo)
        {
            var img = await OsuDrawerCommon.GetCacheOrDownload(b.ImageUrl);
            img.ResizeY(80);

            if (bX + img.Width > ImageWidth - MarginX)
            {
                badges.Insert(0, new Image<Rgba32>(ImageWidth, 120).Clear(Color.FromRgb(42, 34, 38)));
                bX = MarginX;
            }

            badges[0].DrawImageVCenter(img, bX);

            bX += bMargin + img.Width;
        }

        return badges;
    }

    private static async Task<Image<Rgba32>> GetPPlusChart(string mode, long osuId)
    {
        Font font;
        var  pPlusBorderPen = new Pen(Color.Gray, 2);
        var  penColor       = Color.FromRgb(255, 204, 51).ToPixel<Rgba32>();
        var  pPlusChartPen  = new Pen(penColor, 4);

        var pPlusChart       = new Image<Rgba32>(ImageWidth / 3, (int)Math.Ceiling(Math.Sqrt(3) / 2 * ImageWidth / 3) + (int)pPlusBorderPen.StrokeWidth);
        var pPlusChartCenter = new PointF(pPlusChart.Width / 2, pPlusChart.Height / 2);

        var hexagon1 = ShapeDraw.BuildHexagon(pPlusChartCenter, pPlusChart.Width / 6);
        var hexagon2 = ShapeDraw.BuildHexagon(pPlusChartCenter, pPlusChart.Width / 3);
        var hexagon3 = ShapeDraw.BuildHexagon(pPlusChartCenter, pPlusChart.Width / 2);

        pPlusChart.Mutate(i => i
            .DrawLines(pPlusBorderPen, hexagon3.ToArray())
            .DrawLines(pPlusBorderPen, hexagon2.ToArray())
            .DrawLines(pPlusBorderPen, hexagon1.ToArray())
        );

        foreach (var i in hexagon1.Zip(hexagon3))
        {
            pPlusChart.Mutate(im => im.DrawLines(pPlusBorderPen, i.First, i.Second));
        }

        if (mode == "osu")
        {
            var ppData = (await GetPPlus(osuId)).UserData;
            var ppType = new[] { "acc", "flow", "jump", "pre", "speed", "sta" };
            var data = new List<double>
            {
                ppData.AccuracyTotal,
                ppData.FlowAimTotal,
                ppData.JumpAimTotal,
                ppData.PrecisionTotal,
                ppData.SpeedTotal,
                ppData.StaminaTotal
            };

            const int maxData = 1100;
            var       multi   = new[] { 122.25, 113.35, 106.03, 120.09, 115.45, 116.31 };

            // 让数据差距不至于太大，Log[data] * multi
            var convertedData = data.Zip(multi)
                .Select(d => d.Second * Math.Log(d.First))
                .Select(d => d < 0 ? 0 : d)
                .ToList();

            var deg        = 0.0;
            var dataPoints = new List<PointF>();
            foreach (var length in convertedData.Select(d => d / maxData * (pPlusChart.Width / 2)))
            {
                dataPoints.Add(new PointF((float)(length * Math.Cos(deg) + pPlusChartCenter.X), (float)(length * Math.Sin(deg) + pPlusChartCenter.Y)));
                deg += Math.PI / 3;
            }

            dataPoints.Add(dataPoints[0]);

            // 填充雷达图的折线内部
            var polygon = new Polygon(new LinearLineSegment(dataPoints.ToArray()));
            pPlusChart.Mutate(i => i
                .DrawLines(pPlusChartPen, dataPoints.ToArray())
                .Fill(Color.FromRgba(penColor.R, penColor.G, penColor.B, 50), polygon)
            );

            // 画顶点、画 pp+ 的类型和值
            var idx = 0;
            font = new Font(FontExo2, 50);

            //扩展一下图片，防止文本画出边界
            pPlusChart = new Image<Rgba32>(pPlusChart.Width * 2, pPlusChart.Height + 40)
                .DrawImage(pPlusChart, pPlusChart.Width / 2, 20)
                .CloneAs<Rgba32>();

            foreach (var d in data.Zip(ppType))
            {
                var location = new PointF(
                    20 * (float)Math.Cos(Math.PI * idx / 3) + dataPoints[idx].X + pPlusChart.Width / 4,
                    20 * (float)Math.Sin(Math.PI * idx / 3) + dataPoints[idx].Y + 20);

                var option = ImageDraw.GetTextOptions(font, location);

                if (idx is 2 or 3 or 4)
                {
                    option.HorizontalAlignment = HorizontalAlignment.Right;
                }

                option.VerticalAlignment = idx switch
                {
                    1 or 2 => VerticalAlignment.Top,
                    0 or 3 => VerticalAlignment.Center,
                    4 or 5 => VerticalAlignment.Bottom,
                    _      => option.VerticalAlignment
                };

                var ellipsePolygon = new EllipsePolygon(new PointF(dataPoints[idx].X + pPlusChart.Width / 4, dataPoints[idx].Y + 20), 6);

                pPlusChart.Mutate(i => i
                    .DrawText(option, $"{d.Second}: {d.First:F0}", pPlusChartPen.StrokeFill)
                    .Fill(penColor, ellipsePolygon)
                );

                idx += 1;
            }
        }
        else
        {
            var pPlusDisabled = new Image<Rgba32>(pPlusChart.Width * 3 / 4, 150)
                .Clear(Color.FromRgba(0, 0, 0, 200))
                .RoundCorners(40);

            const string text = "PP+数据不可用";
            font = new Font(FontYaHei, 60);

            pPlusDisabled.DrawTextCenter(text, font, Color.White);

            pPlusChart.DrawImageCenter(pPlusDisabled);
        }

        return pPlusChart;
    }
}