using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
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
    private static string TempPath => ConfigurationManager.Configuration.Osu.TempPath;
    private static string ResourcePath => ConfigurationManager.Configuration.Osu.ResourcePath;

    private static async Task<PPlus> GetPPlus(long uid)
    {
        var filename = $"pp+cache-{uid}-{DateTime.Now:yyyy-MM}.json";
        var path     = Path.Join(TempPath, filename);

        if (File.Exists(path))
        {
            return PPlus.FromJson(await File.ReadAllTextAsync(path));
        }

        var pPlus = await OsuApi.GetPPlusJsonById(uid);

        await File.WriteAllTextAsync(path, pPlus);

        return PPlus.FromJson(pPlus);
    }

    private static async Task<Image> GetCacheOrDownload(Uri uri)
    {
        var ext = uri.ToString().Split('.').Last();

        var filename = uri.ToString().GetSha256Hash() + '.' + ext;

        return await GetCacheOrDownload(filename, uri);
    }

    private static async Task<Image> GetCacheOrDownload(string filename, Uri uri)
    {
        var filepath = Path.Join(TempPath, filename);
        if (File.Exists(filepath))
        {
            return (await Image.LoadAsync(filepath)).CloneAs<Rgba32>();
        }

        var bytes = await uri.GetBytesAsync();
        await File.WriteAllBytesAsync(filepath, bytes);

        return (await Image.LoadAsync(filepath)).CloneAs<Rgba32>();
    }

    private static async Task<Image> GetBanner(this OsuUserInfo info)
    {
        return await GetCacheOrDownload(info.Cover.Url);
    }

    private static Image GetMode(string mode)
    {
        return Image.Load(Path.Join(ResourcePath, $"mode-{mode}.png")).CloneAs<Rgba32>();
    }

    private static Image GetIcon(string iconName)
    {
        return Image.Load(Path.Join(ResourcePath, $"icon-{iconName}.png")).CloneAs<Rgba32>();
    }

    private static async Task<Image> GetAvatar(this OsuUserInfo info)
    {
        return await GetCacheOrDownload(info.AvatarUrl);
    }

    public static async Task<Image> GetImage(this OsuUserInfo info)
    {
        const int imageWidth = 2000;
        const int marginX    = 50;

        Debug.Assert(info.RankHistory != null);

        var fontFamily = SystemFonts.Get("Exo 2");
        var fontYaHei  = SystemFonts.Get("Microsoft YaHei");

        #region Header & game mode

        var header = new Image<Rgba32>(imageWidth, 110);

        // 头
        header.Clear(Color.FromRgb(61, 41, 50));

        // 标题
        var font = new Font(fontYaHei, 40, FontStyle.Regular);

        var userIcon = GetIcon("user").ResizeX(80);
        header.DrawImage(userIcon, marginX, (header.Height - userIcon.Height) / 2);

        const string headTitle = "玩家信息";

        var titleX = marginX + userIcon.Width + 10;
        var titleY = (header.Height - headTitle.MeasureWithSpace(font).Height) / 2;
        header.DrawText(headTitle, font, Color.White, titleX, titleY);

        // 游玩模式
        var mode = GetMode(info.RankHistory.Mode).ResizeY(60);
        header.DrawImage(mode, header.Width - marginX - mode.Width, (header.Height - mode.Height) / 2);

        #endregion

        #region avatar & banner

        const int bannerMaxHeight = 600;

        var banner = (await info.GetBanner()).ResizeX(imageWidth);

        if (banner.Height > bannerMaxHeight)
        {
            banner = banner.Crop(0, (banner.Height - bannerMaxHeight) / 2, imageWidth, bannerMaxHeight);
        }

        var nameBanner = new Image<Rgba32>(imageWidth, 170 + banner.Height);

        nameBanner.Clear(Color.FromRgb(56, 46, 50));

        // banner
        nameBanner.DrawImage(banner, 0, 0);

        // avatar
        var avatar = (await info.GetAvatar()).Resize(240, 240).RoundCorners(80);
        nameBanner.DrawImage(avatar, marginX, nameBanner.Height - 20 - avatar.Height);

        var nameCard = new Image<Rgba32>(1000, 140);

        font = new Font(fontFamily, 48, FontStyle.Bold);

        // username
        var (_, _, nameWidth, nameHeight) = info.Username.MeasureWithSpace(font);
        nameCard.DrawText(info.Username, font, Color.White, 0, 0);

        // supporter
        if (info.IsSupporter)
        {
            var fontSupporter = new Font(fontFamily, 36, FontStyle.Regular);
            var supporterChar = new string('♥', info.SupportLevel);

            var supportColor = Color.FromRgb(255, 102, 171);
            nameCard.DrawText(supporterChar, fontSupporter, supportColor, nameWidth, 10);
        }

        // region flag
        var regionIcon = GetIcon(info.Region.Code.ToLower())
            .ResizeY(50)
            .RoundCorners(12);
        nameCard.DrawImage(regionIcon, 2, (int)(nameHeight + 12));

        // region name
        font = new Font(fontFamily, 32, FontStyle.Bold);
        var regionName   = info.Region.Name + (string.IsNullOrWhiteSpace(info.Title) ? "" : $" // {info.Title}");
        var regionHeight = regionName.MeasureWithSpace(font).Height;
        nameCard.DrawText(regionName, font, Color.White, regionIcon.Width + 10, (nameCard.Height - nameHeight - regionHeight) / 2 + nameHeight);

        var nameCardX = marginX + avatar.Width + 20;
        var nameCardY = (nameBanner.Height - banner.Height - nameCard.Height) / 2 + banner.Height;
        nameBanner.DrawImage(nameCard, nameCardX, nameCardY);

        // level
        var levelIcon  = GetIcon("level").ResizeY(100);
        var levelIconX = nameBanner.Width - marginX - levelIcon.Width;
        var levelIconY = (nameBanner.Height - banner.Height - levelIcon.Height) / 2 + banner.Height;
        // 等级的框
        nameBanner.DrawImage(levelIcon, levelIconX, levelIconY);

        var levelFont   = new Font(fontFamily, 40, FontStyle.Bold);
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

        var progressF = new Font(fontFamily, 24, FontStyle.Regular);
        var progressS = info.Statistics.Level.Progress.ToString("N0") + '%';
        var progressM = progressS.MeasureWithSpace(progressF);
        var progressX = levelBarX + levelBar.Width - progressM.Width;
        var progressY = levelBarY + levelBar.Height + 5;
        nameBanner.DrawText(progressS, progressF, Color.White, progressX, progressY);

        #endregion

        #region 排名、历史rank、ss数量

        var detailWidth = imageWidth / 2 - 150;

        var rank = new Image<Rgba32>(detailWidth, 100);

        const string rankText1 = "全球排名";
        const string rankText2 = "国内/区内排名";

        var font1 = new Font(fontYaHei, 24, FontStyle.Regular);
        var font2 = new Font(fontFamily, 60, FontStyle.Bold);

        var fontColor = Color.FromRgb(240, 219, 228);

        var text1H = rankText1.MeasureWithSpace(font1).Height - 8;
        rank.DrawText(rankText1, font1, Color.White, 2, 0);

        var rank1  = $"#{info.Statistics.GlobalRank:N0}";
        var text1W = Math.Max(rankText1.MeasureWithSpace(font1).Width, rank1.MeasureWithSpace(font2).Width);
        rank.DrawText(rank1, font2, fontColor, 0, text1H);

        rank.DrawText(rankText2, font1, Color.White, text1W + marginX, 0);
        rank.DrawText($"#{info.Statistics.RegionRank:N0}", font2, fontColor, text1W + marginX, text1H);

        // rank history
        var chart = new Image<Rgba32>(detailWidth, 120);

        var history = info.RankHistory.Data.ToArray();
        var min     = history.Any() ? history.Min() : 0;
        var max     = history.Any() ? history.Max() : 0;

        var points         = new List<PointF>();
        var rankHistoryPen = new Pen(Color.FromRgb(255, 204, 34), 4);

        for (var i = 0; i < history.Length; i++)
        {
            var xNew = (float)i / history.Length * chart.Width;
            var yNew = (float)(history[i] - min) / (max - min) * (chart.Height - 8) + 4;

            points.Add(new PointF(xNew, yNew));
        }

        chart.Mutate(i => i
            .DrawLines(rankHistoryPen, max != min
                ? points.ToArray()
                : new[] { new PointF(0, chart.Height / 2), new PointF(chart.Width, chart.Height / 2) })
        );

        // counter 奖章、pp、游戏时间、ss个数等
        var counter = new Image<Rgba32>(detailWidth, 74);

        font1 = new Font(fontYaHei, 24, FontStyle.Regular);
        font2 = new Font(fontFamily, 32, FontStyle.Bold);
        var font3 = new Font(fontFamily, 24, FontStyle.Bold);

        fontColor = Color.FromRgb(240, 219, 228);

        const string text1 = "奖章";
        const string text2 = "pp";
        const string text3 = "游戏时间";

        // 奖章
        text1H = text1.MeasureWithSpace(font1).Height;
        counter.DrawText(text1, font1, Color.White, 0, 5);
        counter.DrawText($"{info.UserAchievements.Length:N0}", font2, fontColor, 0, text1H);

        // pp
        counter.DrawText(text2, font1, Color.White, 70, 0);
        counter.DrawText($"{info.Statistics.Pp:N0}", font2, fontColor, 70, text1H);

        // 游戏时间
        var text3X = 70 + info.Statistics.Pp.ToString("N0").MeasureWithSpace(font2).Width + 20;
        counter.DrawText(text3, font1, Color.White, text3X, 5);
        var t = TimeSpan.FromSeconds(info.Statistics.PlayTime);
        counter.DrawText($"{t.Days:N0}d {t.Hours:N0}h {t.Minutes:N0}m", font2, fontColor, text3X, text1H);

        // ss个数、s个数等
        const int iconHeight = 44;
        const int iconWidth  = 88;

        var icons = new Dictionary<string, Image>
        {
            { "a", GetIcon("rank-a").ResizeY(iconHeight) },
            { "s", GetIcon("rank-s").ResizeY(iconHeight) },
            { "sh", GetIcon("rank-s-s").ResizeY(iconHeight) },
            { "ss", GetIcon("rank-ss").ResizeY(iconHeight) },
            { "ssh", GetIcon("rank-ss-s").ResizeY(iconHeight) }
        };

        var rankX = counter.Width - iconWidth;

        foreach (var rk in new[] { "a", "s", "sh", "ss", "ssh" })
        {
            var s = info.Statistics.GradeCounts[rk].ToString("N0");
            counter.DrawImage(icons[rk], rankX, 0);
            var w = s.Measure(font1).Width;
            counter.DrawText(s, font3, Color.White, rankX + (iconWidth - w) / 2, iconHeight);

            rankX -= (int)Math.Max(w, iconWidth) + 5;
        }

        #endregion

        #region 统计信息：Ranked 谱面总分, 准确率, 游戏次数, 回放被观看次数, 总命中次数, 最大连击, 总分

        font1 = new Font(fontYaHei, 32, FontStyle.Regular);
        font2 = new Font(fontFamily, 36, FontStyle.Bold);

        var textHeader = new[]
        {
            "Ranked 谱面总分", "准确率", "游戏次数", "回放被观看次数", "总命中次数", "最大连击", "总分"
        };

        var st = info.Statistics;
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
        var summary      = new Image<Rgba32>((int)summaryWidth + xGap, rank.Height + chart.Height + counter.Height + 40);

        var textHeight = (float)summary.Height / textHeader.Length;
        var lineSpace  = (int)(textHeight - textHeader[0].MeasureWithSpace(font).Height) / 2;


        for (var i = 0; i < textHeader.Length; i++)
        {
            summary.DrawText(textHeader[i], font1, Color.White, 0, lineSpace + textHeight * i);
        }

        for (var i = 0; i < value.Length; i++)
        {
            var option = ImageDraw.GetTextOptions(font2, new PointF(summary.Width, lineSpace + textHeight * i));

            option.HorizontalAlignment = HorizontalAlignment.Right;

            var text = value[i];
            summary.Mutate(im => im.DrawText(option, text, fontColor));
        }

        #endregion

        #region PP+

        var pPlusBorderPen = new Pen(Color.Gray, 2);
        var penColor       = Color.FromRgb(255, 204, 51).ToPixel<Rgba32>();
        var pPlusChartPen  = new Pen(penColor, 4);

        var pPlusChart       = new Image<Rgba32>(imageWidth / 3, (int)Math.Ceiling(Math.Sqrt(3) / 2 * imageWidth / 3) + (int)pPlusBorderPen.StrokeWidth);
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

        if (info.RankHistory.Mode == "osu")
        {
            var ppData = (await GetPPlus(info.Id)).UserData;
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
            var convertedData = data.Zip(multi).Select(d => d.Second * Math.Log(d.First)).ToList();

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
            font = new Font(fontFamily, 50);

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
            var hitImage = new Image<Rgba32>(pPlusChart.Width * 3 / 4, 150)
                .Clear(Color.FromRgba(0, 0, 0, 200))
                .RoundCorners(40);

            const string text = "PP+数据不可用";
            font = new Font(fontYaHei, 60);
            var measure = text.MeasureWithSpace(font);

            hitImage.DrawText(text, font, Color.White, (hitImage.Width - measure.Width) / 2, (hitImage.Height - measure.Height) / 2);

            pPlusChart.DrawImage(hitImage, (pPlusChart.Width - hitImage.Width) / 2, (pPlusChart.Height - hitImage.Height) / 2);
        }

        #endregion

        // 把上面几个小卡片拼接起来
        var userDetail = new Image<Rgba32>(imageWidth, summary.Height + 60);

        var gap = (userDetail.Height - (rank.Height + chart.Height + counter.Height) - 20) / 2;

        userDetail.Clear(Color.FromRgb(42, 34, 38));
        pPlusChart.ResizeY(userDetail.Height - 80);

        var pen1 = new Pen(Color.FromRgb(28, 23, 25), 2);

        const int lineGap      = 30;
        const int marginBorder = 10;

        var xSummary = userDetail.Width - marginX - summary.Width;
        var xLine1   = marginX + Math.Max(rank.Width, Math.Max(chart.Width, counter.Width)) + lineGap;
        var xLine2   = xSummary - lineGap;
        var xPPlus   = ((xLine2 - lineGap - (xLine1 + lineGap)) - pPlusChart.Width) / 2 + xLine1 + lineGap;

        var detail = userDetail;
        userDetail.Mutate(i => i
            .DrawImage(rank, marginX, marginBorder)
            .DrawImage(chart, marginX, marginBorder + rank.Height + gap)
            .DrawImage(counter, marginX, marginBorder + rank.Height + chart.Height + gap + gap)
            .DrawLines(pen1, new PointF(xLine1, marginBorder), new PointF(xLine1, detail.Height - marginBorder))
            .DrawLines(pen1, new PointF(xLine2, marginBorder), new PointF(xLine2, detail.Height - marginBorder))
            .DrawImage(pPlusChart, xPPlus, (detail.Height - pPlusChart.Height) / 2)
            .DrawImage(summary, detail.Width - summary.Width - marginX, (detail.Height - summary.Height) / 2)
        );

        // 狗牌
        if (info.Badges.Any())
        {
            var badges = new List<Image> { new Image<Rgba32>(imageWidth, 120).Clear(Color.FromRgb(42, 34, 38)) };

            var       bX      = marginX;
            const int bMargin = 20;

            foreach (var b in info.Badges)
            {
                var img = await GetCacheOrDownload(b.ImageUrl);
                img.ResizeY(80);

                if (bX + img.Width > imageWidth - marginX)
                {
                    badges.Insert(0, new Image<Rgba32>(imageWidth, 120).Clear(Color.FromRgb(42, 34, 38)));
                    bX = marginX;
                }

                badges[0].DrawImage(img, bX, 20);

                bX += bMargin + img.Width;
            }

            userDetail = badges.Aggregate(userDetail, (current, badge) =>
                new Image<Rgba32>(current.Width, current.Height + badge.Height)
                    .DrawImage(current, 0, badge.Height).DrawImage(badge, 0, 0).CloneAs<Rgba32>()
            );
        }

        // 把它们拼起来
        var bg = new Image<Rgba32>(imageWidth, header.Height + nameBanner.Height + userDetail.Height);

        var pen2 = new Pen(Color.FromRgb(37, 30, 34), 1);
        bg.Mutate(i => i
            .DrawImage(header, 0, 0)
            .DrawImage(nameBanner, 0, header.Height)
            .DrawLines(pen2, new PointF(0, header.Height + nameBanner.Height), new PointF(imageWidth, header.Height + nameBanner.Height))
            .DrawImage(userDetail, 0, header.Height + nameBanner.Height)
        );

        return bg.RoundCorners(40);
    }
}