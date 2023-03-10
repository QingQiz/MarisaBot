using System.Numerics;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using ScottPlot;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;
using Path = System.IO.Path;
using SDColor = System.Drawing.Color;
using VerticalAlignment = SixLabors.Fonts.VerticalAlignment;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuScoreDrawer
{
    private static async Task<Image> GetCover(this OsuScore score)
    {
        var cover = score.Beatmap.TryGetCover();

        if (cover != null && !File.Exists(cover))
        {
            var p = OsuApi.GetBeatmapPath(score.Beatmap);
            Directory.Delete(Path.GetDirectoryName(p)!, true);
            // re-download
            cover = score.Beatmap.TryGetCover();
        }

        return await Image.LoadAsync(cover);
    }

    private const int ImageWidth = 2000;
    private const int MarginX = 100;

    private static readonly Color BgColor = Color.FromRgb(46, 53, 56);

    public static Image Distribution(this OsuScore[] scores)
    {
        const int pieSize = 400;

        Image GeneratePie(IEnumerable<double> values, IEnumerable<string> labels, string title)
        {
            var plt = new Plot(pieSize, pieSize);

            var pie = plt.AddPie(values.ToArray());
            pie.DonutLabel  = title;
            pie.DonutSize   = 0.5;
            pie.SliceLabels = labels.ToArray();
            plt.Legend();

            return plt.GetBitmap().ToImageSharpImage<Rgba32>();
        }

        var modeInt = scores.GroupBy(s => s.ModeInt).MaxBy(x => x.Count())!.Key;

        var pies = new List<Image>();

        switch (modeInt)
        {
            case 3:
            {
                // keys
                {
                    var g      = scores.GroupBy(x => (int)x.Beatmap.Cs).OrderBy(x => x.Key).ToList();
                    var values = g.Select(x => x.Count());
                    var labels = g.Select(x => $"{x.Key}K");
                    pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Keys"));
                }
                // pp acc
                {
                    var acc = scores.Select(x => (int)(x.PpAccuracy * 100)).OrderByDescending(x => x).ToList();

                    var g = new List<IGrouping<int, int>>();

                    var conv = 1;

                    while (!g.Any() || g.Count > 10)
                    {
                        g    =  acc.GroupBy(x => x / conv).ToList();
                        conv *= 2;
                    }

                    var values = g.Select(x => x.Count());
                    var labels = g.Select(x => $"> {x.Key * conv / 2}%");
                    pies.Add(GeneratePie(values.Select(x => (double)x), labels, "PP Acc"));
                }
                break;
            }
            case 0:
            {
                // ar
                {
                    var g      = scores.GroupBy(x => (int)x.Beatmap.Ar).OrderBy(x => x.Key).ToList();
                    var values = g.Select(x => x.Count());
                    var labels = g.Select(x => $"{x.Key}.X");
                    pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Ar"));
                }
                break;
            }
            default:
            {
                // od
                {
                    var g      = scores.GroupBy(x => (int)x.Beatmap.Accuracy).OrderBy(x => x.Key).ToList();
                    var values = g.Select(x => x.Count());
                    var labels = g.Select(x => $"{x.Key}.X");
                    pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Od"));
                }
                break;
            }
        }

        // acc
        {
            var acc = scores.Select(x => (int)(x.Accuracy * 100)).OrderByDescending(x => x).ToList();

            var g = new List<IGrouping<int, int>>();

            var conv = 1;

            while (!g.Any() || g.Count > 10)
            {
                g    =  acc.GroupBy(x => x / conv).ToList();
                conv *= 2;
            }

            var values = g.Select(x => x.Count());
            var labels = g.Select(x => $"> {x.Key * conv / 2}%");
            pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Acc"));
        }

        // stars
        {
            var starRating = scores.AsParallel().Select(x => x.StarRating() * 10).OrderByDescending(x => x).ToList();

            var g = new List<IGrouping<int, double>>();

            var conv = 1;

            while (!g.Any() || g.Count > 6)
            {
                g    =  starRating.GroupBy(x => (int)x / conv).ToList();
                conv *= 2;
            }

            var values = g.Select(x => x.Count());
            var labels = g.Select(x => $"> {x.Key * conv / 10.0 / 2:F2}★");
            pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Star"));
        }

        // mods
        {
            var mods = scores
                .Select(x => x.Mods).SelectMany(x => x).OrderBy(x => x)
                .GroupBy(x => x).ToList();

            var values = mods.Select(x => x.Count()).ToList();
            var labels = mods.Select(x => x.Key).ToList();

            values.Add(scores.Count(x => !x.Mods.Any()));
            labels.Add("NoMod");

            if (values.Any())
            {
                pies.Add(GeneratePie(values.Select(x => (double)x), labels, "Mod"));
            }
        }

        var img = new Image<Rgba32>(pieSize * pies.Count, pieSize * 4);

        for (var i = 0; i < pies.Count; i++)
        {
            img.DrawImage(pies[i], i * pieSize, pieSize * 3);
        }

        {
            var plt = new Plot(pieSize * pies.Count, pieSize);

            var s2 = scores.Where(s => s.Passed).OrderBy(x => x.CreatedAt).ToList();

            var ys = s2.Select(x => x.Pp ?? 0).ToArray();
            var xs = s2.Select(x => x.CreatedAt.DateTime.ToOADate()).ToArray();

            var x1 = xs[0];
            var x2 = xs[^1];

            var model = new ScottPlot.Statistics.LinearRegressionLine(xs, ys);

            plt.XAxis.DateTimeFormat(true);
            plt.AddScatter(xs, ys, lineWidth: 0,
                markerShape: MarkerShape.filledCircle, color: System.Drawing.Color.FromArgb(127, 233, 190, 53), markerSize: 10);
            var linePlot = plt.AddLine(model.slope, model.offset, (x1, x2),
                lineWidth: 2, color: System.Drawing.Color.FromArgb(127, 140, 39, 167));
            linePlot.LineStyle = LineStyle.Dash;

            plt.XAxis.Line(false);
            plt.YAxis.Line(false);
            plt.XAxis2.Hide();
            plt.YAxis2.Hide();

            img.DrawImage(plt.GetBitmap().ToImageSharpImage<Rgba32>(), 0, pieSize * 2);
        }

        // 分布
        {
            const int binSize = 3;

            var plt = new Plot(pieSize * pies.Count, pieSize * 2);

            var values = scores.AsParallel().Select(x => x.PerformancePoint()).ToArray();

            // 统计图
            var (counts, binEdges) = ScottPlot.Statistics.Common.Histogram(values, min: values.Min() - binSize, max: values.Max() + binSize, binSize: binSize);
            var leftEdges = binEdges.Take(binEdges.Length - 1).ToArray();

            // bar plot
            var bar = plt.AddBar(values: counts, positions: leftEdges);
            bar.BarWidth        = 3;
            bar.BorderLineWidth = 1;
            bar.FillColor       = System.Drawing.ColorTranslator.FromHtml("#9bc3eb");

            // 极值分布
            var xs       = DataGen.Range(start: binEdges.First(), stop: binEdges.Last(), step: 0.1, includeStop: true);
            var pdfParam = MathExt.FitExtremeValueDistribution(values);
            var pdf      = MathExt.ExtremeValueDistribution(pdfParam.alpha, pdfParam.beta);
            var ys       = xs.Select(x => pdf(x) * 100).ToArray();

            var probPlot = plt.AddScatterLines(
                xs: xs,
                ys: ys,
                lineWidth: 2,
                label: "probability");
            probPlot.YAxisIndex = 1;
            plt.YAxis2.Ticks(true);

            // display vertical lines at points of interest
            var stats = new ScottPlot.Statistics.BasicStats(values);

            plt.AddVerticalLine(stats.Mean, SDColor.Black, 2, LineStyle.Solid, "mean");
            plt.AddVerticalLine(pdfParam.alpha, SDColor.Black, 2, LineStyle.Dash, "alpha");

            plt.AddVerticalLine(stats.Mean - stats.StDev, SDColor.Black, 2, LineStyle.Dot, "SD");
            plt.AddVerticalLine(stats.Mean + stats.StDev, SDColor.Black, 2, LineStyle.Dot);

            plt.AddVerticalLine(stats.Min, SDColor.Gray, 1, LineStyle.Dash, "min/max");
            plt.AddVerticalLine(stats.Max, SDColor.Gray, 1, LineStyle.Dash);

            plt.Legend(location: Alignment.UpperRight);

            // customize the plot style
            plt.Title("Best Performance Distribution", size: 40);
            plt.YAxis.Label("Count (#)");
            plt.YAxis2.Label("Probability (%)");
            plt.SetAxisLimits(yMin: 0);
            plt.SetAxisLimits(yMin: 0, yAxisIndex: 1);

            img.DrawImage(plt.GetBitmap().ToImageSharpImage<Rgba32>(), 0, 0);
        }

        img.DrawText("Generate By QingQiz/MarisaBot", OsuDrawerCommon.FontExo2.CreateFont(18), Color.Gray, 10, 10);

        return img;
    }

    public static Image GetMiniCards(this List<(OsuScore, int)> score)
    {
        const int gap    = 5;
        const int margin = 20;

        var ims = score.Select(x => GetMiniCard(x.Item1)).ToList();

        var image =
            new Image<Rgba32>(margin * 2 + ims[0].Width, margin * 2 + ims.Count * ims[0].Height + (ims.Count - 1) * gap).Clear(Color.ParseHex("#382e32"));

        var drawY = margin;

        var font = OsuDrawerCommon.FontExo2.CreateFont(16);

        for (var i = 0; i < ims.Count; i++)
        {
            image.DrawImage(ims[i], margin, drawY);
            image.DrawText($"#{score[i].Item2 + 1}", font, Color.White, margin + 10, drawY + 5);
            drawY += ims[i].Height + gap;
        }

        return image;
    }

    private static Image GetMiniCard(this OsuScore score)
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
            var font = OsuDrawerCommon.FontExo2.CreateFont(28);

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
            var font1 = OsuDrawerCommon.FontExo2.CreateFont(40, FontStyle.Bold);
            var font2 = OsuDrawerCommon.FontExo2.CreateFont(30);

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

            var font = OsuDrawerCommon.FontExo2.CreateFont(40, FontStyle.Bold);

            var opt = ImageDraw.GetTextOptions(font);

            opt.HorizontalAlignment = HorizontalAlignment.Right;
            opt.Origin              = new Vector2(width - rec.Width - gap, (height - truePp.MeasureWithSpace(font).Height) / 2);

            im.DrawText(opt, truePp, Color.White);
        }

        {
            var acc    = $"{score.Accuracy * 100:F2}%";
            var weight = $"权重：{score.Weight?.Percentage ?? 0:F0}%";

            var font1 = OsuDrawerCommon.FontExo2.CreateFont(33, FontStyle.Bold);
            var font2 = OsuDrawerCommon.FontYaHei.CreateFont(28);

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

            var icons = score.Mods.Select(OsuModDrawer.GetModIconWithoutText);

            foreach (var i in icons)
            {
                var draw = i.ResizeX(modIconWidth);
                modIconDrawX -= draw.Width;

                im.DrawImageVCenter(draw, modIconDrawX);
                modIconDrawX -= iconGap;
            }
        }

        {
            var font = OsuDrawerCommon.FontExo2.CreateFont(35);

            var text = score.Beatmapset.TitleUnicode + " by " + score.Beatmapset.ArtistUnicode;

            while (text.MeasureWithSpace(font).Width + marginX + rankIcon.Width + gap > modIconDrawX)
            {
                text = text[..^4] + "...";
            }

            im.DrawText(text, font, Color.White, marginX + rankIcon.Width + gap, marginY);
        }


        return im.RoundCorners(15);
    }

    public static Image CompareWith(this OsuScore[] scoreSet, OsuScore[] anotherScoreSet)
    {
        const int width = 1000, height = 400;

        Image DrawCompare(double[] data1, double[] data2, string title, string xLabel, double binSize, bool gumbelDist = false)
        {
            var plt = new Plot(width, height);

            var min = Math.Min(data1.Min(), data2.Min());
            var max = Math.Max(data1.Max(), data2.Max());

            min -= binSize;
            max += binSize;

            // 统计
            var (cnt1, binEdges) = ScottPlot.Statistics.Common.Histogram(data1, min: min, max: max, binSize: binSize);
            var (cnt2, _)        = ScottPlot.Statistics.Common.Histogram(data2, min: min, max: max, binSize: binSize);
            var leftEdges = binEdges.Take(binEdges.Length - 1).ToArray();

            // bar plot
            var bar1 = plt.AddBar(values: cnt1, positions: leftEdges);
            bar1.BarWidth        = binSize;
            bar1.FillColor       = System.Drawing.Color.FromArgb(50, System.Drawing.Color.Blue);
            bar1.BorderLineWidth = 0;

            var bar2 = plt.AddBar(values: cnt2, positions: leftEdges);
            bar2.BarWidth        = binSize;
            bar2.FillColor       = System.Drawing.Color.FromArgb(50, System.Drawing.Color.Red);
            bar2.BorderLineWidth = 0;

            double[] pdf1data, pdf2data;

            var xs = DataGen.Range(start: binEdges.First(), stop: binEdges.Last(), step: 0.1, includeStop: true);

            // 分布
            if (gumbelDist)
            {
                var pdf1Param = MathExt.FitExtremeValueDistribution(data1);
                var pdf1      = MathExt.ExtremeValueDistribution(pdf1Param.alpha, pdf1Param.beta);
                pdf1data = xs.Select(x => pdf1(x) * 100).ToArray();

                var pdf2Param = MathExt.FitExtremeValueDistribution(data2);
                var pdf2      = MathExt.ExtremeValueDistribution(pdf2Param.alpha, pdf2Param.beta);
                pdf2data = xs.Select(x => pdf2(x) * 100).ToArray();
            }
            else
            {
                pdf1data = ScottPlot.Statistics.Common.ProbabilityDensity(data1, xs, true);
                pdf2data = ScottPlot.Statistics.Common.ProbabilityDensity(data2, xs, true);
            }

            // 画分布
            var dist1 = plt.AddScatterLines(
                xs: xs,
                ys: pdf1data,
                color: System.Drawing.Color.FromArgb(150, System.Drawing.Color.Blue),
                lineWidth: 3,
                label: $"你：{scoreSet.First().User.Username}");
            dist1.YAxisIndex = 1;

            var dist2 = plt.AddScatterLines(
                xs: xs,
                ys: pdf2data,
                color: System.Drawing.Color.FromArgb(150, System.Drawing.Color.Red),
                lineWidth: 3,
                label: $"别人：{anotherScoreSet.First().User.Username}");
            dist2.YAxisIndex = 1;
            plt.YAxis2.Ticks(true);

            plt.Title(title);
            plt.XLabel(xLabel);
            plt.Legend(location: gumbelDist ? Alignment.UpperRight : Alignment.UpperLeft);
            plt.SetAxisLimits(yMin: 0);
            plt.SetAxisLimits(yMin: 0, yAxisIndex: 1);

            return plt.GetBitmap().ToImageSharpImage<Rgba32>();
        }

        var im1 = DrawCompare(
            scoreSet.Select(s => s.Pp).Cast<double>().ToArray(),
            anotherScoreSet.Select(s => s.Pp).Cast<double>().ToArray(),
            "PP Comparison", "PP", 5, true
        );

        Image im2;

        // mania 使用 pp acc
        if (scoreSet.First().Mode == OsuApi.ModeList.Last())
        {
            im2 = DrawCompare(
                scoreSet.Select(s => s.PpAccuracy * 100).ToArray(),
                anotherScoreSet.Select(s => s.PpAccuracy * 100).ToArray(),
                "PP Acc Comparison", "PP Acc", 0.5
            );
        }
        else
        {
            im2 = DrawCompare(
                scoreSet.Select(s => s.Accuracy * 100).ToArray(),
                anotherScoreSet.Select(s => s.Accuracy * 100).ToArray(),
                "Acc Comparison", "Acc", 0.5
            );
        }

        return new Image<Rgba32>(im1.Width, im1.Height + im2.Height).DrawImage(im1, 0, 0).DrawImage(im2, 0, im1.Height);
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
        var beatmapDetail = OsuBeatmapDrawer.GetBeatmapDetail(score.Beatmap);
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

        var pp = score.PerformancePoint().ToString("F2");

        var cards1 = new List<Image>();

        {
            var w = score.ModeInt != 3 ? (ImageWidth - staCardGap * 2) / 3 : (ImageWidth - staCardGap * 3) / 4;

            cards1.Add(GetKeyValuePair("准确率", $"{score.Accuracy * 100:F2}%", w));

            if (score.ModeInt == 3)
            {
                cards1.Add(GetKeyValuePair("PP Acc.", $"{score.PpAccuracy * 100:F2}%", w));
            }

            cards1.Add(GetKeyValuePair("最大连击", $"{score.MaxCombo:N0}x", w));
            cards1.Add(GetKeyValuePair("PP", pp, w));
        }

        var cards2 = new List<Image>();

        switch (score.ModeInt)
        {
            case 3:
            {
                const int width = (ImageWidth - staCardGap * 5) / 6;

                cards2.AddRange(new[]
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

                cards2.AddRange(new[]
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

                cards2.AddRange(new[]
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

                cards2.AddRange(new[]
                {
                    GetKeyValuePair("FRUITS", $"{score.Statistics.Count300:N0}", width),
                    GetKeyValuePair("TICKS", $"{score.Statistics.Count100:N0}", width),
                    GetKeyValuePair("DRP MISS", $"{score.Statistics.CountKatu:N0}", width),
                    GetKeyValuePair("MISS", $"{score.Statistics.CountMiss:N0}", width),
                });
                break;
            }
        }

        var sta = new Image<Rgba32>(ImageWidth, cards1[0].Height + cards2[0].Height + staCardVGap).Clear(BgColor);

        for (var i = 0; i < cards1.Count; i++)
        {
            sta.DrawImage(cards1[i], (cards1[0].Width + staCardGap) * i, 0);
        }

        for (var i = 0; i < cards2.Count; i++)
        {
            sta.DrawImage(cards2[i], (cards2[0].Width + staCardGap) * i, cards1[0].Height + staCardVGap);
        }

        return sta;
    }

    private static Image<Rgba32> GetKeyValuePair(string key, string value, int width)
    {
        const int gap = 10;

        var fKey   = OsuDrawerCommon.FontYaHei.CreateFont(60);
        var fValue = OsuDrawerCommon.FontExo2.CreateFont(100);

        var mKey   = key.MeasureWithSpace(fKey);
        var mValue = value.MeasureWithSpace(fValue);

        var imageHeader = new Image<Rgba32>(width, (int)mKey.Height).Clear(Color.ParseHex("#171a1c"));

        imageHeader.DrawTextCenter(key, fKey, Color.White);

        var image = new Image<Rgba32>(width, (int)(imageHeader.Height + mValue.Height + gap));

        image.DrawImage(imageHeader.RoundCorners(imageHeader.Height / 2), 0, 0);
        image.DrawTextHCenter(value, fValue, Color.White, imageHeader.Height + gap);

        return image;
    }


    private static Image<Rgba32> GetGradeAndMods(int maxWidth, long grade, string[] mods, DateTimeOffset time)
    {
        var gradeText = grade.ToString("N0");
        var timeText  = (time + TimeSpan.FromHours(8)).ToString("// yyyy-MM-dd hh:mm:ss");

        var card = new Image<Rgba32>(maxWidth, 500);

        var font1 = OsuDrawerCommon.FontExo2.CreateFont(160);
        var font3 = OsuDrawerCommon.FontExo2.CreateFont(40);

        var icons = mods.Select(OsuModDrawer.GetModIcon);

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
        var statusMark = OsuBeatmapDrawer.GetBeatmapStatusIcon(beatmap.Status);

        songInfo.DrawImage(statusMark, songInfoDrawX, songInfoDrawY + 10);

        // song name
        var songNameFont    = OsuDrawerCommon.FontExo2.CreateFont(60);
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

        var starRating        = score.StarRating();
        var starRatingText    = $"★ {starRating:F2}";
        var starRatingFont    = OsuDrawerCommon.FontExo2.CreateFont(35, FontStyle.Bold);
        var starRatingMeasure = starRatingText.MeasureWithSpace(starRatingFont);

        var starRatingImg = new Image<Rgba32>((int)starRatingMeasure.Width + starRatingPaddingX * 2, songTypeSize);

        starRatingImg
            .Clear(OsuBeatmapDrawer.GetStarRatingColor(starRating))
            .DrawTextCenter(starRatingText, starRatingFont, starRating < 9 ? Color.Black : Color.White)
            .RoundCorners(starRatingImg.Height / 2);

        songInfo.DrawImage(starRatingImg, songInfoDrawX, songInfoDrawY);

        // level name
        var mapperName = $"谱师：{beatmapset.Creator}";
        var mapperFont = OsuDrawerCommon.FontExo2.CreateFont(45, FontStyle.Bold);

        var mapperWidth = mapperName.MeasureWithSpace(mapperFont).Width;

        songInfoDrawX += starRatingImg.Width + elementGap;

        var levelName = beatmap.Version;
        var levelFont = OsuDrawerCommon.FontExo2.CreateFont(45);

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
}