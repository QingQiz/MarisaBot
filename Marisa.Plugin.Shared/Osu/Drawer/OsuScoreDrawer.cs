using System.Numerics;
using Marisa.Plugin.Shared.Osu.Entity.Score;
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

    public static Image GetAccRing(string rank, double acc, int modeInt, bool withText = true)
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

        if (!withText) return accRing;

        var im = new Image<Rgba32>(300, 300);

        rank = rank.Trim('h').Trim('H');
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);
        im.Mutate(i => i.GaussianBlur(15));
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);

        accRing.DrawImageCenter(im);

        return accRing;
    }
}