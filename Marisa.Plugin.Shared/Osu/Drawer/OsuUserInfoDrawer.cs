using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuUserInfoDrawer
{
    private static string TempPath => ConfigurationManager.Configuration.Osu.TempPath;
    private static string ResourcePath => ConfigurationManager.Configuration.Osu.ResourcePath;

    private static async Task<Bitmap> GetCacheOrDownload(string filename, Uri uri)
    {
        var filepath = Path.Join(TempPath, filename);
        if (File.Exists(filepath))
        {
            return (Bitmap)Image.FromFile(filepath);
        }

        var bytes = await uri.GetBytesAsync();
        await File.WriteAllBytesAsync(filepath, bytes);

        return (Bitmap)Image.FromFile(filepath);
    }

    public static async Task<Bitmap> GetCover(this OsuUserInfo info)
    {
        var coverPath = info.Cover.CustomUrl == null
            ? $"C{info.Cover.Id}.jpg"
            : info.Cover.CustomUrl.Split('/').Last();

        return await GetCacheOrDownload(coverPath, info.Cover.Url);
    }

    public static Bitmap GetMode(string mode)
    {
        return (Bitmap)Image.FromFile(Path.Join(ResourcePath, $"mode-{mode}.png"));
    }

    public static Bitmap GetIcon(string iconName)
    {
        return (Bitmap)Image.FromFile(Path.Join(ResourcePath, $"icon-{iconName}.png"));
    }

    public static async Task<Bitmap> GetAvatar(this OsuUserInfo info)
    {
        var avatarName = info.AvatarUrl.Query[1..];

        return await GetCacheOrDownload(avatarName, info.AvatarUrl);
    }

    public static async Task<Bitmap> GetImage(this OsuUserInfo info)
    {
        const int    imageWidth     = 1000;
        const string fontFamily     = "Torus";
        const string fontFamilyBold = "Torus SemiBold";

        var header = new Bitmap(imageWidth, 55);

        // 头
        using (var g = Graphics.FromImage(header))
        {
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            g.Clear(Color.FromArgb(61, 41, 50));

            // 标题
            var font     = new Font("Microsoft YaHei", 20, FontStyle.Regular, GraphicsUnit.Pixel);
            var userIcon = GetIcon("user").ResizeX(40);
            g.DrawImage(userIcon, 50, (header.Height - userIcon.Height) / 2);

            const string headTitle = "玩家信息";

            var titleX = 50 + userIcon.Width + 5;
            var titleY = (header.Height - g.MeasureString(headTitle, font).Height) / 2;
            g.DrawString(headTitle, font, Brushes.White, titleX, titleY);

            // 游玩模式
            var mode = GetMode(info.Playmode).ResizeY(30);
            g.DrawImage(mode, header.Width - 50 - mode.Width, (header.Height - mode.Height) / 2);
        }

        // cover image
        var cover = (await info.GetCover()).ResizeX(imageWidth);
        if (cover.Height > 250)
        {
            cover = cover.Crop(0, (cover.Height - 250) / 2, imageWidth, 250);
        }

        // name and avatar
        var nameBanner = new Bitmap(imageWidth, 85 + cover.Height);
        using (var g = Graphics.FromImage(nameBanner))
        {
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            g.Clear(Color.FromArgb(56, 46, 50));

            //cover
            g.DrawImage(cover, 0, 0);

            // avatar
            var avatar = (await info.GetAvatar()).Resize(120, 120).RoundCorners(40);
            g.DrawImage(avatar, 50, nameBanner.Height - 10 - avatar.Height);

            var nameCard = new Bitmap(750, 55);
            using (var gName = Graphics.FromImage(nameCard))
            {
                gName.CompositingQuality = CompositingQuality.HighQuality;
                gName.InterpolationMode  = InterpolationMode.HighQualityBicubic;
                gName.SmoothingMode      = SmoothingMode.HighQuality;
                gName.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

                var font = new Font(fontFamilyBold, 24, FontStyle.Bold, GraphicsUnit.Pixel);

                // username
                var nameMeasure = gName.MeasureString(info.Username, font);
                var nameHeight  = nameMeasure.Height;
                gName.DrawString(info.Username, font, Brushes.White, 0, 0);

                // supporter
                if (info.IsSupporter)
                {
                    var nameWidth     = nameMeasure.Width;
                    var fontSupporter = new Font(fontFamily, 18, FontStyle.Regular, GraphicsUnit.Pixel);
                    var supporterChar = new string('♥', info.SupportLevel);

                    var supportColor = Color.FromArgb(255, 102, 171);
                    gName.DrawString(supporterChar, fontSupporter, new SolidBrush(supportColor), nameWidth, 5);
                }

                // region flag
                var regionIcon = GetIcon(info.Region.Code.ToLower())
                    .ResizeY(nameCard.Height - (int)nameHeight - 10)
                    .RoundCorners(5);
                gName.DrawImage(regionIcon, 2, nameHeight + 5);

                // region name
                font = new Font(fontFamily, 16, FontStyle.Bold, GraphicsUnit.Pixel);
                var regionHeight = gName.MeasureString(info.Region.Name, font).Height;
                gName.DrawString(info.Region.Name, font, Brushes.White,
                    regionIcon.Width + 6, (nameCard.Height - nameHeight - regionHeight) / 2 + nameHeight);
            }

            var nameCardX = 50 + avatar.Width + 20;
            var nameCardY = (nameBanner.Height - cover.Height - nameCard.Height) / 2 + cover.Height;
            g.DrawImage(nameCard, nameCardX, nameCardY);

            // level
            var levelIcon  = GetIcon("level").ResizeY(50);
            var levelIconX = nameBanner.Width - 50 - levelIcon.Width;
            var levelIconY = (nameBanner.Height - cover.Height - levelIcon.Height) / 2 + cover.Height;
            // 等级的框
            g.DrawImage(levelIcon, levelIconX, levelIconY);

            var levelFont    = new Font(fontFamilyBold, 20, FontStyle.Bold, GraphicsUnit.Pixel);
            var levelString  = info.Statistics.Level.Current.ToString("N0");
            var levelStringM = g.MeasureString(levelString, levelFont);
            var levelStringH = levelStringM.Height;
            var levelStringW = levelStringM.Width;
            var levelStringX = levelIconX + (levelIcon.Width - levelStringW) / 2;
            var levelStringY = levelIconY + (levelIcon.Height - levelStringH) / 2;
            // 等级的值
            g.DrawString(levelString, levelFont, Brushes.White, levelStringX, levelStringY);

            var levelBar = new Bitmap(200, 6);
            using (var gb = Graphics.FromImage(levelBar))
            {
                gb.Clear(Color.FromArgb(28, 23, 25));
                var bounds = new Rectangle(0, 0, levelBar.Width * info.Statistics.Level.Progress / 100, levelBar.Height);

                var diameter = levelBar.Height;
                var size     = new Size(diameter, diameter);
                var arc      = new Rectangle(bounds.Location, size);
                var path     = new GraphicsPath();

                // top left arc  
                path.AddArc(arc, 180, 90);
                // top right arc  
                arc.X = bounds.Right - diameter;
                path.AddArc(arc, 270, 90);
                // bottom right arc  
                arc.Y = bounds.Bottom - diameter;
                path.AddArc(arc, 0, 90);
                // bottom left arc 
                arc.X = bounds.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                
                var color = Color.FromArgb(255, 102, 171);
                gb.FillPath(new SolidBrush(color), path);
                gb.DrawPath(new Pen(color, 1), path);
            }
            levelBar = levelBar.RoundCorners(3);

            var levelBarX = levelIconX - 15 - levelBar.Width;
            var levelBarY = levelIconY + (levelIcon.Height - levelBar.Height) / 2;
            // 等级的进度条
            g.DrawImage(levelBar, levelBarX, levelBarY);

            var progressF = new Font(fontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel);
            var progressS = info.Statistics.Level.Progress.ToString("N0") + '%';
            var progressM = g.MeasureString(progressS, progressF);
            var progressX = levelBarX + levelBar.Width - progressM.Width;
            var progressY = levelBarY + levelBar.Height + 5;
            g.DrawString(progressS, progressF, Brushes.White, progressX, progressY);
        }

        // rank and region rank
        var rank = new Bitmap(690, 42);
        using (var g = Graphics.FromImage(rank))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            const string rankText1 = "全球排名";
            const string rankText2 = "国内/区内排名";

            var font1 = new Font("Microsoft YaHei", 12, FontStyle.Regular, GraphicsUnit.Pixel);
            var font2 = new Font(fontFamily, 30, FontStyle.Bold, GraphicsUnit.Pixel);

            var color = new SolidBrush(Color.FromArgb(240, 219, 228));

            var text1H = g.MeasureString(rankText1, font1).Height - 8;
            g.DrawString(rankText1, font1, Brushes.White, 2, 0);

            var rank1  = $"#{info.Statistics.GlobalRank:N0}";
            var text1W = Math.Max(g.MeasureString(rankText1, font1).Width, g.MeasureString(rank1, font2).Width);
            g.DrawString(rank1, font2, color, 2, text1H);

            g.DrawString(rankText2, font1, Brushes.White, text1W + 20, 0);
            g.DrawString($"#{info.Statistics.RegionRank:N0}", font2, color, text1W + 20, text1H);
        }

        // rank history
        var chart = new Bitmap(690, 60);
        using (var g = Graphics.FromImage(chart))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            var history = info.RankHistory.Data.Reverse().ToArray();
            var min     = history.Min();
            var max     = history.Max();

            var pen = new Pen(Color.FromArgb(255, 204, 34), 2);

            if (max != min)
            {
                var x = -1.0f;
                var y = -1.0f;
                for (var i = 0; i < history.Length; i++)
                {
                    var xNew = (float)i / history.Length * chart.Width;
                    var yNew = chart.Height - (float)(history[i] - min) / (max - min) * (chart.Height - 4) - 2;

                    if (x < 0 || y < 0)
                    {
                        x = xNew;
                        y = yNew;
                    }
                    else
                    {
                        g.DrawLine(pen, x, y, xNew, yNew);
                        x = xNew;
                        y = yNew;
                    }
                }
            }
            else
            {
                g.DrawLine(pen, 0, chart.Height / 2, chart.Width, chart.Height / 2);
            }
        }

        // counter 奖章、pp、游戏时间、ss个数等
        var counter = new Bitmap(690, 37);
        using (var g = Graphics.FromImage(counter))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            var font1 = new Font(fontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel);
            var font2 = new Font(fontFamily, 16, FontStyle.Bold, GraphicsUnit.Pixel);
            var font3 = new Font(fontFamilyBold, 12, FontStyle.Bold, GraphicsUnit.Pixel);

            var color = new SolidBrush(Color.FromArgb(240, 219, 228));

            const string text1 = "奖章";
            const string text2 = "pp";
            const string text3 = "游戏时间";

            // 奖章
            var text1H = g.MeasureString(text1, font1).Height - 2;
            g.DrawString(text1, font1, Brushes.White, 2, 0);
            g.DrawString($"{info.UserAchievements.Length:N0}", font2, color, 2, text1H);

            // pp
            g.DrawString(text2, font1, Brushes.White, 70, 0);
            g.DrawString($"{info.Statistics.Pp:N0}", font2, color, 70, text1H);

            // 游戏时间
            g.DrawString(text3, font1, Brushes.White, 150, 0);
            var t = TimeSpan.FromSeconds(info.Statistics.PlayTime);
            g.DrawString($"{t.Days:N0}d {t.Hours:N0}h {t.Minutes:N0}m", font2, color, 150, text1H);

            // ss个数、s个数等
            const int iconHeight = 22;
            const int iconWidth  = 44;

            var icons = new Dictionary<string, Bitmap>
            {
                { "a", GetIcon("rank-a").ResizeY(iconHeight) },
                { "s", GetIcon("rank-s").ResizeY(iconHeight) },
                { "sh", GetIcon("rank-s-s").ResizeY(iconHeight) },
                { "ss", GetIcon("rank-ss").ResizeY(iconHeight) },
                { "ssh", GetIcon("rank-ss-s").ResizeY(iconHeight) }
            };

            float x = counter.Width - iconWidth;

            foreach (var rk in new[] { "a", "s", "sh", "ss", "ssh" })
            {
                var s = info.Statistics.GradeCounts[rk].ToString("N0");
                g.DrawImage(icons[rk], x, 0);
                var w = g.MeasureString(s, font1).Width;
                g.DrawString(s, font3, Brushes.White, x + (iconWidth - w) / 2, iconHeight);

                x -= Math.Max(w, iconWidth) + 5;
            }
        }

        // 统计信息：总分、准确率之类的
        var summary = new Bitmap(210, 130);
        using (var g = Graphics.FromImage(summary))
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var font = new Font(fontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel);

            var textHeader = new[]
            {
                "Ranked 谱面总分", "准确率", "游戏次数", "总分", "总命中次数", "最大连击", "回放被观看次数"
            };

            var st = info.Statistics;
            var value = new[]
            {
                st.RankedScore.ToString("N0"), st.HitAccuracy.ToString("F2") + "%", st.PlayCount.ToString("N0"),
                st.TotalScore.ToString("N0"), st.TotalHits.ToString("N0"), st.MaximumCombo.ToString("N0"),
                st.ReplaysWatchedByOthers.ToString("N0")
            };


            var textHeight = (float)summary.Height / textHeader.Length;
            var padding    = (textHeight - g.MeasureString(textHeader[0], font).Height) / 2;

            var headerWidth = 0f;

            for (var i = 0; i < textHeader.Length; i++)
            {
                headerWidth = Math.Max(headerWidth, g.MeasureString(textHeader[i], font).Width);
                g.DrawString(textHeader[i], font, Brushes.White, 0, padding + textHeight * i);
            }

            headerWidth += 10;

            for (var i = 0; i < value.Length; i++)
            {
                g.DrawString(value[i], font, Brushes.White, headerWidth, padding + textHeight * i);
            }
        }

        // 把上面几个小卡片拼接起来
        var userDetail = new Bitmap(imageWidth, 190);
        using (var g = Graphics.FromImage(userDetail))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            g.Clear(Color.FromArgb(42, 34, 38));

            var padding = (userDetail.Height - (rank.Height + chart.Height + counter.Height) - 20) / 2;

            g.DrawImage(rank, 50, 10);
            g.DrawImage(chart, 50, 10 + rank.Height + padding);
            g.DrawImage(counter, 50, 10 + rank.Height + chart.Height + padding + padding);

            var pen = new Pen(Color.FromArgb(28, 23, 25), 2);
            g.DrawLine(pen, 50 + chart.Width + 15, 10, 50 + chart.Width + 14, userDetail.Height - 10);

            g.DrawImage(summary, 50 + chart.Width + 30, (userDetail.Height - 20 - summary.Height) / 2 + 10);
        }

        // 把它们拼起来
        var bg = new Bitmap(imageWidth, header.Height + nameBanner.Height + userDetail.Height);
        using (var g = Graphics.FromImage(bg))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode      = SmoothingMode.HighQuality;

            g.DrawImage(header, 0, 0);
            g.DrawImage(nameBanner, 0, header.Height);
            var pen = new Pen(Color.FromArgb(37, 30, 34), 1);
            g.DrawLine(pen, 0, header.Height + nameBanner.Height, imageWidth, header.Height + nameBanner.Height);
            g.DrawImage(userDetail, 0, header.Height + nameBanner.Height);
        }

        return bg.RoundCorners(20);
    }
}